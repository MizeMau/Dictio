using Dictio.Twitch.Events;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Dictio.Twitch
{
    public class TwitchEventSubWebSocket
    {
        private readonly ClientWebSocket _client;
        /// <summary>
        /// The tutorial to create the token can be found in this video
        /// https://www.youtube.com/watch?v=CSh1EspfOf4
        /// </summary>
        private readonly string _oauthToken;   // Your OAuth token (Bearer)
        private readonly string _clientId;     // Your Twitch app client_id
        private readonly string _broadcasterId; // Channel ID to watch follows for
        private readonly string _moderatorId;   // Usually same as broadcaster, unless you want another moderator

        public Task ReaderWorkerTask;

        public EventHandler<TwitchChatMessage> OnMessageRecieved;
        public EventHandler<TwitchFollower> OnFollowerRecieved;

        public TwitchEventSubWebSocket()
        {
            _client = new ClientWebSocket();

            string? oauthToken = Environment.GetEnvironmentVariable("TwitchChat")
                ?.Trim();
            if (string.IsNullOrEmpty(oauthToken))
            {
                throw new ArgumentException("OAuth token is not set in the environment variable 'TwitchChat'.");
            }
            _oauthToken = oauthToken;
            string? clientId = Environment.GetEnvironmentVariable("TwitchCliend")
                ?.Trim();
            if (string.IsNullOrEmpty(clientId))
            {
                throw new ArgumentException("OAuth token is not set in the environment variable 'TwitchChat'.");
            }
            _clientId = clientId;

            _broadcasterId = "441477997";
            _moderatorId = _broadcasterId;

            Connect().GetAwaiter().GetResult();
            ReaderWorkerTask = Task.Run(ReaderWorker);
        }

        public async Task Connect()
        {
            var uri = new Uri("wss://eventsub.wss.twitch.tv/ws");
            await _client.ConnectAsync(uri, CancellationToken.None);
            Console.WriteLine("[INFO] Connected to Twitch EventSub WebSocket.");
        }

        private async Task ReaderWorker()
        {
            var buffer = new byte[8192];

            while (_client.State == WebSocketState.Open)
            {
                var segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result;
                try
                {
                    result = await _client.ReceiveAsync(segment, CancellationToken.None);
                }
                catch (WebSocketException wsex)
                {
                    Console.WriteLine("[ERROR] WebSocket error: " + wsex.Message);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("[INFO] Server closed connection.");
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (message.Contains("session_keepalive"))
                    continue;
                Console.WriteLine("[DEBUG] Message received:");
                Console.WriteLine(message);

                try
                {
                    using var doc = JsonDocument.Parse(message);
                    var root = doc.RootElement;
                    string messageType = root.GetProperty("metadata").GetProperty("message_type").GetString();

                    if (messageType == "session_welcome")
                    {
                        var sessionId = root
                            .GetProperty("payload")
                            .GetProperty("session")
                            .GetProperty("id")
                            .GetString();

                        Console.WriteLine($"[INFO] Session Welcome received. Session ID: {sessionId}");
                        Console.WriteLine("[INFO] Subscribing to channel.follow + channel.chat.message...");

                        await SubscribeToChannelFollow(sessionId);
                        await SubscribeToChannelChatMessage(sessionId);
                    }
                    else if (messageType == "notification")
                    {
                        HandleMessage(root.GetProperty("payload"));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERROR] Failed to parse JSON: " + ex.Message);
                }
            }
        }
        private void HandleMessage(JsonElement payload)
        {
            var subscriptionType = payload
                            .GetProperty("subscription")
                            .GetProperty("type")
                            .GetString();

            JsonElement eventData;
            switch (subscriptionType)
            {
                case "channel.chat.message":
                    eventData = payload.GetProperty("event");
                    var twitchChatMessage = JsonSerializer.Deserialize<TwitchChatMessage>(eventData);
                    OnMessageRecieved.Invoke(this, twitchChatMessage);

                    Console.WriteLine($"[CHAT] {twitchChatMessage.ChatterUserName}: {twitchChatMessage.Message}");
                    break;
                case "channel.follow":
                    eventData = payload.GetProperty("event");
                    var twitchFollower = JsonSerializer.Deserialize<TwitchFollower>(eventData);
                    OnFollowerRecieved.Invoke(this, twitchFollower);

                    Console.WriteLine($"[FOLLOW] {twitchFollower.UserName} followed!");
                    break;
            }
        }

        private async Task SubscribeToChannelFollow(string sessionId)
        {
            await CreateSubscription(new
            {
                type = "channel.follow",
                version = "2",
                condition = new
                {
                    broadcaster_user_id = _broadcasterId,
                    moderator_user_id = _moderatorId
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId
                }
            });
        }

        private async Task SubscribeToChannelChatMessage(string sessionId)
        {
            await CreateSubscription(new
            {
                type = "channel.chat.message",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = _broadcasterId,
                    user_id = _moderatorId, // can be same as broadcaster if you want your own chat
                    moderator_user_id = _moderatorId
                },
                transport = new
                {
                    method = "websocket",
                    session_id = sessionId
                }
            });
        }

        private async Task CreateSubscription(object body)
        {
            using var httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _oauthToken);
            httpClient.DefaultRequestHeaders.Add("Client-Id", _clientId);

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://api.twitch.tv/helix/eventsub/subscriptions", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[INFO] Successfully subscribed to {((JsonElement)JsonSerializer.SerializeToElement(body)).GetProperty("type").GetString()}");
            }
            else
            {
                Console.WriteLine($"[ERROR] Subscription failed: {response.StatusCode}");
            }

            Console.WriteLine("[DEBUG] Subscription response:");
            Console.WriteLine(responseString);
        }
    }
}
