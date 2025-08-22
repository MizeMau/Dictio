using Dictio.Twitch;

namespace Dictio
{
    internal class Program
    {
        private static Twitch.Client _client;
        private static Aspectus.WebSocket _websocket;
        static void Main(string[] args)
        {
            _client = new Twitch.Client("mizemauu");
            _client.OnMessageRecieved += OnMessageReceived;

            _websocket = new Aspectus.WebSocket();
            Task.Delay(Timeout.Infinite).Wait();
        }

        private static void OnMessageReceived(object? sender, IRCMessage ircMessagge)
        {
            _websocket.SendMessage(ircMessagge);
        }
    }
}