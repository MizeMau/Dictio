using Dictio.Twitch;

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
            //_client = new Twitch.Client(channel);
            //_client.OnMessageRecieved += OnMessageReceived;
            _twitchEventSubWebSocket = new TwitchEventSubWebSocket();
            _twitchEventSubWebSocket.OnMessageRecieved += OnMessageReceived;

            _websocket = new Websites.WebSocket();

            _tts = new TTS.F5ttsClient();
            _tts.PlayText($"Listening to {channel}").GetAwaiter().GetResult();

            Task.Delay(Timeout.Infinite).Wait();
        }

        private static void OnMessageReceived(object? sender, TwitchChatMessage twitchChatMessage)
        {
            _websocket.SendMessage(twitchChatMessage);
            string message = "";
            foreach (var twitchChatMessageFragments in twitchChatMessage.Message.Fragments)
            {
                if (twitchChatMessageFragments.Type != "text") continue;
                message += twitchChatMessageFragments.Text;
            }
            _tts.PlayText(message).GetAwaiter().GetResult();
        }
    }
}