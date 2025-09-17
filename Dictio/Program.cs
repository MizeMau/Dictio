using Dictio.Twitch;
using Dictio.Twitch.Events;
using Newtonsoft.Json;

namespace Dictio
{
    internal class Program
    {
        private static Twitch.TwitchEventSubWebSocket _twitchEventSubWebSocket;
        private static Websites.WebSocket _websocket;
        private static TTS.F5ttsClient _tts;
        static void Main(string[] args)
        {
            string channel = "mizemauu";
            _twitchEventSubWebSocket = new TwitchEventSubWebSocket();
            _twitchEventSubWebSocket.OnMessageRecieved += OnMessageReceived;
            _twitchEventSubWebSocket.OnFollowerRecieved += OnFollowerReceived;

            _websocket = new Websites.WebSocket();

            _tts = new TTS.F5ttsClient();
            _tts.PlayText($"Listening to {channel}").GetAwaiter().GetResult();

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
            }
        }
        private static void OnMessageReceived(object? sender, TwitchChatMessage twitchChatMessage)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchChatMessage);
            _websocket.SendMessage(messageJSON);
            string message = "";
            foreach (var twitchChatMessageFragments in twitchChatMessage.Message.Fragments)
            {
                if (twitchChatMessageFragments.Type != "text") continue;
                message += twitchChatMessageFragments.Text;
            }
            _tts.PlayText(message).GetAwaiter().GetResult();
        }

        private static void OnFollowerReceived(object? sender, TwitchFollower twitchFollower)
        {
            string messageJSON = JsonConvert.SerializeObject(twitchFollower);
            _websocket.SendMessage(messageJSON);

            string message = $"{twitchFollower.UserName} followed!";
            _tts.PlayText(message).GetAwaiter().GetResult();
        }
    }
}