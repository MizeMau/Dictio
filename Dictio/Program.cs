using Dictio.TTS;
using Dictio.Twitch;
using Dictio.Twitch.Events;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Dictio
{
    internal class Program
    {
        private static Twitch.TwitchEventSubWebSocket _twitchEventSubWebSocket;
        private static Websites.WebSocket _websocket;
        private static TtsClient _ttsClient;

        private static float _exaggeration = 1f;
        private static float _cfgWeight = 0.05f;
        private static float _temperature = 0.45f;
        private static float _topP = 0.87f;

        static async Task Main(string[] args)
        {
            var settings = Settings.ReadSettings();
            if (settings == null)
            {
                Console.Clear();
                Console.WriteLine("Enter your BroadcasterID (if you dont know what your ID is then input nothing):");
                string broadcasterID = Console.ReadLine() ?? "";
                if (broadcasterID == "")
                {
                    Process.Start(new ProcessStartInfo("https://www.streamweasels.com/tools/convert-twitch-username-%20to-user-id/") { UseShellExecute = true });
                    Console.WriteLine("You can get the Broadcaster ID on this website, input the Broadcaster ID now:");
                    broadcasterID = Console.ReadLine() ?? "";
                }
                Console.WriteLine("Do you want to use TTS? (y/n):");
                string useTTSInput = Console.ReadLine() ?? "n";
                bool useTTS = useTTSInput.ToLower() == "y";
                settings = new Settings.Model
                {
                    BroadcasterID = broadcasterID,
                    UseTTS = useTTS
                };
                Settings.WriteSettings(settings);
            }

            using var server = new ServerManager();
            if (settings.UseTTS)
            {
                await server.StartAsync();

                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\n[App] Shutting down …");
                    server.Dispose();
                    Environment.Exit(0);
                };

                _ttsClient = new TtsClient(server.BaseUrl);

                await _ttsClient.Say($"Listening to Broadcaster {settings!.BroadcasterID}");
            }

            _twitchEventSubWebSocket = new TwitchEventSubWebSocket(settings!.BroadcasterID);
            _twitchEventSubWebSocket.OnMessageRecieved += OnMessageRecieved;
            _twitchEventSubWebSocket.OnMessageDeleteRecieved += OnMessageDeleteRecieved;
            _twitchEventSubWebSocket.OnFollowerRecieved += OnFollowerRecieved;
            _twitchEventSubWebSocket.OnRaidRecieved += OnRaidRecieved;

            _websocket = new Websites.WebSocket();

#if DEBUG
            _ = Task.Run(Commands);
#endif
            await Task.Delay(Timeout.Infinite);
        }

        private static async Task Commands()
        {
            while (true)
            {
                string input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;
                await _ttsClient.Say(input, _exaggeration, _cfgWeight, _temperature, _topP);
            }
        }
        private async static void OnMessageRecieved(object? sender, TwitchChatMessage twitchChatMessage)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchChatMessage);
            _websocket.SendMessage(messageJSON);
            string message = "";
            foreach (var twitchChatMessageFragments in twitchChatMessage.Message.Fragments)
            {
                if (twitchChatMessageFragments.Type != "text") continue;
                message += twitchChatMessageFragments.Text;
            }
            await _ttsClient.Say(message, _exaggeration, _cfgWeight, _temperature, _topP);
        }
        private async static void OnMessageDeleteRecieved(object? sender, TwitchChatMessageDelete twitchChatMessageDelete)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchChatMessageDelete);
            _websocket.SendMessage(messageJSON);
        }
        private async static void OnFollowerRecieved(object? sender, TwitchFollower twitchFollower)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchFollower);
            _websocket.SendMessage(messageJSON);

            string message = $"{twitchFollower.UserName} followed!";
            await _ttsClient.Say(message, _exaggeration, _cfgWeight, _temperature, _topP);
        }
        private async static void OnRaidRecieved(object? sender, TwitchRaid twitchRaid)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchRaid);
            _websocket.SendMessage(messageJSON);

            string message = $"{twitchRaid.FromBroadcasterUserName} Raided the stream with {twitchRaid.Viewers} Views!";
            await _ttsClient.Say(message, _exaggeration, _cfgWeight, _temperature, _topP);
        }
    }
}