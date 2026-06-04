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
        private static TTS.F5ttsClient _tts;
        static void Main(string[] args)
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

            _twitchEventSubWebSocket = new TwitchEventSubWebSocket(settings!.BroadcasterID);
            _twitchEventSubWebSocket.OnMessageRecieved += OnMessageRecieved;
            _twitchEventSubWebSocket.OnMessageDeleteRecieved += OnMessageDeleteRecieved;
            _twitchEventSubWebSocket.OnFollowerRecieved += OnFollowerRecieved;
            _twitchEventSubWebSocket.OnRaidRecieved += OnRaidRecieved;

            _websocket = new Websites.WebSocket();

            if (settings.UseTTS)
            {
                _tts = new TTS.F5ttsClient();
                _tts.PlayText($"Listening to Broadcaster {settings!.BroadcasterID}").GetAwaiter().GetResult();
            }

#if DEBUG
            _ = Task.Run(Commands);
#endif

            Task.Delay(Timeout.Infinite).Wait();
        }
        private static async Task Commands()
        {
            while (true)
            {
                string input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;
                var test = new TwitchFollower();
                test.UserName = "thisIsWA";
                OnFollowerRecieved(null, test);
            }
        }
        private static void OnMessageRecieved(object? sender, TwitchChatMessage twitchChatMessage)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchChatMessage);
            _websocket.SendMessage(messageJSON);
            string message = "";
            foreach (var twitchChatMessageFragments in twitchChatMessage.Message.Fragments)
            {
                if (twitchChatMessageFragments.Type != "text") continue;
                message += twitchChatMessageFragments.Text;
            }
            _tts?.PlayText(message).GetAwaiter().GetResult();
        }
        private static void OnMessageDeleteRecieved(object? sender, TwitchChatMessageDelete twitchChatMessageDelete)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchChatMessageDelete);
            _websocket.SendMessage(messageJSON);
        }
        private static void OnFollowerRecieved(object? sender, TwitchFollower twitchFollower)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchFollower);
            _websocket.SendMessage(messageJSON);

            string message = $"{twitchFollower.UserName} followed!";
            _tts?.PlayText(message).GetAwaiter().GetResult();
        }
        private static void OnRaidRecieved(object? sender, TwitchRaid twitchRaid)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchRaid);
            _websocket.SendMessage(messageJSON);

            string message = $"{twitchRaid.FromBroadcasterUserName} Raided the stream with {twitchRaid.Viewers} Views!";
            _tts?.PlayText(message).GetAwaiter().GetResult();
        }
    }
}