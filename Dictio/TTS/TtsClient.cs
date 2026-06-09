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

            var payload = new TtsRequest(text, exaggeration, cfgWeight, temperature, topP);

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

        public async Task Say(string text,
            float exaggeration = 0.4f,
            float cfgWeight = 0.7f,
            float temperature = 0.8f,
            float topP = 0.95f)
        {
            var wav = await SynthesiseAsync(text, exaggeration, cfgWeight, temperature, topP);
            AudioPlayer.Play(wav);
        }

        public void Dispose() => _http.Dispose();

        // -----------------------------------------------------------------------
        // DTOs
        // -----------------------------------------------------------------------
        public record TtsRequest(
            [property: JsonPropertyName("text")] string Text,
            [property: JsonPropertyName("exaggeration")] float Exaggeration,
            [property: JsonPropertyName("cfg_weight")] float CfgWeight,
            [property: JsonPropertyName("temperature")] float Temperature,
            [property: JsonPropertyName("top_p")] float TopP);
        public record TtsResponse(
            [property: JsonPropertyName("audio_b64")] string AudioB64,
            [property: JsonPropertyName("sample_rate")] int SampleRate,
            [property: JsonPropertyName("device")] string Device);
        public record HealthInfo(
            [property: JsonPropertyName("status")] string Status,
            [property: JsonPropertyName("device")] int Device,
            [property: JsonPropertyName("cuda_device_name")] int CudaDeviceName,
            [property: JsonPropertyName("model_loaded")] int ModelLoaded,
            [property: JsonPropertyName("voice_wav")] int VoiceWav,
            [property: JsonPropertyName("voice_wav_exists")] string VoiceWavExists);
    }

}
