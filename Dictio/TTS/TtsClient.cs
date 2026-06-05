using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

namespace Dictio.TTS
{
    /// <summary>
    /// HTTP client for the local Chatterbox Turbo FastAPI server.
    /// All Chatterbox generation parameters are exposed so the Python side
    /// never needs to be touched again.
    /// </summary>
    internal sealed class TtsClient : IDisposable
    {
        private readonly HttpClient _http;

        public TtsClient(string baseUrl)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(120),
            };
        }

        // -----------------------------------------------------------------------
        // /health
        // -----------------------------------------------------------------------
        public async Task<HealthInfo> GetHealthAsync(CancellationToken ct = default)
        {
            var resp = await _http.GetAsync("/health", ct);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<HealthInfo>(ct))!;
        }

        // -----------------------------------------------------------------------
        // /tts — returns raw WAV bytes ready to play or save
        // -----------------------------------------------------------------------
        /// <summary>
        /// Synthesises <paramref name="text"/> and returns WAV bytes.
        /// voice.wav is hardcoded on the server — no path needed here.
        /// Supports Chatterbox Turbo paralinguistic tags in the text:
        ///   [laugh]  [chuckle]  [cough]  [sigh]  [gasp]
        /// </summary>
        /// <param name="text">Text to synthesise (may include paralinguistic tags).</param>
        /// <param name="exaggeration">Emotion / expressiveness 0.0–1.0. Default 0.4.</param>
        /// <param name="cfgWeight">Classifier-free guidance strength. Default 0.7.</param>
        /// <param name="temperature">Sampling temperature 0.1–1.0. Default 0.8.</param>
        /// <param name="topP">Nucleus sampling probability 0.0–1.0. Default 0.95.</param>
        public async Task<byte[]> SynthesiseAsync(
            string text,
            float exaggeration = 0.4f,
            float cfgWeight = 0.7f,
            float temperature = 0.8f,
            float topP = 0.95f,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text must not be empty.", nameof(text));

            var payload = new TtsRequest
            {
                Text = text,
                Exaggeration = exaggeration,
                CfgWeight = cfgWeight,
                Temperature = temperature,
                TopP = topP,
            };

            var resp = await _http.PostAsJsonAsync("/tts", payload, ct);

            if (!resp.IsSuccessStatusCode)
            {
                string body = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"TTS server returned {(int)resp.StatusCode}: {body}");
            }

            var result = (await resp.Content.ReadFromJsonAsync<TtsResponse>(ct))!;
            return Convert.FromBase64String(result.AudioB64);
        }

        public async Task Say(string text)
        {
            var wav = await SynthesiseAsync(text);
            AudioPlayer.Play(wav);
        }

        public void Dispose() => _http.Dispose();

        // -----------------------------------------------------------------------
        // DTOs
        // -----------------------------------------------------------------------
        private sealed class TtsRequest
        {
            [JsonPropertyName("text")] public string Text { get; init; } = "";
            [JsonPropertyName("exaggeration")] public float Exaggeration { get; init; } = 0.4f;
            [JsonPropertyName("cfg_weight")] public float CfgWeight { get; init; } = 0.7f;
            [JsonPropertyName("temperature")] public float Temperature { get; init; } = 0.8f;
            [JsonPropertyName("top_p")] public float TopP { get; init; } = 0.95f;
        }

        private sealed class TtsResponse
        {
            [JsonPropertyName("audio_b64")] public string AudioB64 { get; init; } = "";
            [JsonPropertyName("sample_rate")] public int SampleRate { get; init; }
            [JsonPropertyName("device")] public string Device { get; init; } = "";
        }

        public sealed class HealthInfo
        {
            [JsonPropertyName("status")] public string Status { get; init; } = "";
            [JsonPropertyName("device")] public string Device { get; init; } = "";
            [JsonPropertyName("cuda_device_name")] public string CudaDeviceName { get; init; } = "";
            [JsonPropertyName("model_loaded")] public bool ModelLoaded { get; init; }
            [JsonPropertyName("voice_wav")] public string VoiceWav { get; init; } = "";
            [JsonPropertyName("voice_wav_exists")] public bool VoiceWavExists { get; init; }
        }
    }

}
