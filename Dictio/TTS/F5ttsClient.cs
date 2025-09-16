using Dictio.Twitch;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dictio.TTS
{
    public class F5ttsClient
    {
        private readonly string _ttsURL;
        private readonly HttpClient _client = new HttpClient();
        private readonly List<string> _speakers = new() { "Lina" };// "Nele"

        public F5ttsClient(string url = "http://localhost:7860/gradio_api/call")
        {
            _client = new HttpClient();
            _ttsURL = url;
        }

        public async Task PlayText(string message)
        {
            string speakerName = "";
            foreach (string speaker in _speakers)
            {
                if (message.StartsWith($"({speaker})"))
                {
                    speakerName = speaker;
                    message = message.Replace($"({speaker})", "");
                    break;
                }
            }
            if (string.IsNullOrEmpty(speakerName))
            {
                Random rnd = new();
                int index = rnd.Next(_speakers.Count);
                speakerName = _speakers[index];
            }
            await GenerateAndPlayAsync(message, speakerName);
        }
        public async Task GenerateAndPlayAsync(string message, string speakerName)
        {
            string tempPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string gradioID = "3a12f54b282a5958714bf8201d82151aca739b25d8dfeb31b01ae7ea0d54a631";
            string path = @$"{tempPath}\\Temp\\gradio\\{gradioID}\\{speakerName}.mp3";
            string jsonPayload = CreateRequestBody(path, message);

            string audioPath = await SendToApi(jsonPayload);

            if (string.IsNullOrEmpty(audioPath))
                return;

            PlayAudio(audioPath);
        }
        private string CreateRequestBody(string path, string message)
        {
            var requestBody = new
            {
                data = new object[]
                {
                new {
                    path = path,
                    url = @$"http://localhost:7860/gradio_api/file={path}",
                    meta = new { _type = "gradio.FileData" }
                },
                "",
                $"{message}",
                false,
                true,
                0,
                0.15,
                32,
                1
                }
            };
            return JsonSerializer.Serialize(requestBody);
        }
        private async Task<string> SendToApi(string jsonPayload)
        {
            var postResponse = await _client.PostAsync(
                $"{_ttsURL}/basic_tts",
                new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            );

            postResponse.EnsureSuccessStatusCode();
            var postContent = await postResponse.Content.ReadAsStringAsync();

            string eventId = ExtractEventId(postContent);
            if (string.IsNullOrEmpty(eventId))
                return "";

            var streamUrl = $"{_ttsURL}/basic_tts/{eventId}";
            using var streamResponse = await _client.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead);
            streamResponse.EnsureSuccessStatusCode();

            using var stream = await streamResponse.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data:"))
                    continue;
                try
                {
                    var jsonData = line.Substring(5).Trim();
                    using var doc = JsonDocument.Parse(jsonData);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array &&
                        doc.RootElement.GetArrayLength() > 0 &&
                        doc.RootElement[0].TryGetProperty("path", out var pathProp))
                    {
                        return pathProp.GetString();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse data line: {ex.Message}");
                }
            }
            return "";
        }
        static string ExtractEventId(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 3)
                    return doc.RootElement[3].GetString();
                else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("event_id", out var eventIdProp))
                    return eventIdProp.GetString();
            }
            catch
            {
                var parts = json.Split('"');
                if (parts.Length > 4) return parts[3];
            }
            return "";
        }

        private void PlayAudio(string path)
        {
            using var audioFile = new AudioFileReader(path);
            audioFile.Volume = 0.3f; // Set volume (max: 1.0f)
            using var outputDevice = new WaveOutEvent();
            outputDevice.Init(audioFile);
            outputDevice.Play();

            while (outputDevice.PlaybackState == PlaybackState.Playing)
            {
                Task.Delay(200).Wait();
            }
        }
    }
}
