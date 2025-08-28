using Dictio.Twitch;

namespace Dictio
{
    internal class Program
    {
        private static Twitch.Client _client;
        private static Websites.WebSocket _websocket;
        private static TTS.F5ttsClient _tts;
        static void Main(string[] args)
        {
            string channel = "mizemauu";
            _client = new Twitch.Client(channel);
            _client.OnMessageRecieved += OnMessageReceived;

            _websocket = new Websites.WebSocket();

            _tts = new TTS.F5ttsClient();
            _tts.PlayText($"Listening to {channel}").GetAwaiter().GetResult();

            Task.Delay(Timeout.Infinite).Wait();
        }

        private static void OnMessageReceived(object? sender, IRCMessage ircMessagge)
        {
            _websocket.SendMessage(ircMessagge);
            _tts.PlayText(ircMessagge.Message).GetAwaiter().GetResult();
        }
    }
}