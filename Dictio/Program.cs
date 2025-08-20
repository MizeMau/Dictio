namespace Dictio
{
    internal class Program
    {
        private static Twitch.Cliend _cliend;
        static void Main(string[] args)
        {
            //_cliend = new Twitch.Cliend("mizemauu");
            //_cliend.OnMessageRecieved += OnMessageReceived;
            //await tts.GenerateAndPlayAsync($"Connected! Listening to {channel}...");

            new Aspectus.WebSocket();
            Task.Delay(Timeout.Infinite).Wait();
        }

        static void OnMessageReceived(object sender, Twitch.IRCMessage e)
        {

        }
    }
}