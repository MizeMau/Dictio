using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace Dictio.Twitch
{
    public class BadgeService
    {
        private readonly HttpClient _http;
        private Dictionary<string, string> _badgeCache = new();

        // key format: "set_id/version_id" → image URL
        // e.g. "moderator/1" → "https://static-cdn.jtvnw.net/..."

        public BadgeService(HttpClient http, string clientID, string clientSecretID)
        {
            _http = http;

            string accessToken = GetAppAccessTokenAsync(clientID, clientSecretID).GetAwaiter().GetResult();

            _http.DefaultRequestHeaders.Add("Client-Id", clientID);
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        }

        public async Task LoadAsync(string broadcasterId)
        {
            var global = await FetchBadgesAsync("https://api.twitch.tv/helix/chat/badges/global");
            var channel = await FetchBadgesAsync($"https://api.twitch.tv/helix/chat/badges?broadcaster_id={broadcasterId}");

            // Merge — channel badges win on conflict
            var merged = new Dictionary<string, string>(global);
            foreach (var (key, url) in channel)
                merged[key] = url;

            _badgeCache = merged;
        }

        private async Task<Dictionary<string, string>> FetchBadgesAsync(string url)
        {
            var response = await _http.GetFromJsonAsync<TwitchBadgeResponse>(url);
            var result = new Dictionary<string, string>();

            if (response?.Data == null) return result;

            foreach (var set in response.Data)
                foreach (var version in set.Versions)
                    result[$"{set.SetId}/{version.Id}"] = version.ImageUrl1x;

            return result;
        }
        public async Task<string> GetAppAccessTokenAsync(string clientId, string clientSecret)
        {
            var response = await _http.PostAsync(
                $"https://id.twitch.tv/oauth2/token" +
                $"?client_id={clientId}" +
                $"&client_secret={clientSecret}" +
                $"&grant_type=client_credentials",
                content: null
            );

            var json = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>();
            return json!.AccessToken;
        }


        public string? GetBadgeUrl(string setId, string versionId)
            => _badgeCache.TryGetValue($"{setId}/{versionId}", out var url) ? url : null;
    }

    // DTOs
    public record TwitchBadgeResponse(
        [property: JsonPropertyName("data")] List<TwitchBadgeSet> Data
    );
    public record TwitchBadgeSet(
        [property: JsonPropertyName("set_id")] string SetId,
        [property: JsonPropertyName("versions")] List<TwitchBadgeVersion> Versions
    );
    public record TwitchBadgeVersion(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("image_url_1x")] string ImageUrl1x
    );
    public record TwitchTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );
}
