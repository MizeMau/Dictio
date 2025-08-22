using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Dictio.Twitch
{
    internal class Client
    {
        private readonly string usernameBot;        // Twitch username (lowercase)
        private readonly string channel = "#";           // Channel to join (lowercase, with #)
        /// <summary>
        /// The tutorial to create the token can be found in this video
        /// https://www.youtube.com/watch?v=CSh1EspfOf4
        /// </summary>
        private readonly string oauthToken = "oauth:"; // OAuth token for authentication
        private readonly ClientWebSocket _client;

        public Task ReaderWorkerTask;

        #region events

        public EventHandler<IRCMessage> OnMessageRecieved;

        #endregion
        public Client(string channel, string usernameBot = "mizemauu")
        {
            _client = new ClientWebSocket();
            this.channel += channel.ToLower();
            this.usernameBot = usernameBot.ToLower();

            string? token = Environment.GetEnvironmentVariable("TwitchChat")
                .Trim();
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("OAuth token is not set in the environment variable 'TwitchChat'.");
            }
            this.oauthToken += token;

            Connect().GetAwaiter().GetResult();
            ReaderWorkerTask = Task.Run(ReaderWorker);
        }

        public async Task Connect()
        {
            var ChatURI = new Uri("wss://irc-ws.chat.twitch.tv:443");
            await _client.ConnectAsync(ChatURI, CancellationToken.None);
            Console.WriteLine($"[INFO] Connected! Listening to {channel}...");
            // Send authentication message
            await Send($"PASS {oauthToken}");
            await Send($"NICK {usernameBot}");
            await Send("CAP REQ :twitch.tv/tags twitch.tv/commands twitch.tv/membership");
            await Send($"JOIN {channel}");
        }

        public async Task ReaderWorker()
        {
            var buffer = new byte[4096];

            while (_client.State == WebSocketState.Open)
            {
                var segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result = null;
                try
                {
                    result = await _client.ReceiveAsync(segment, CancellationToken.None);
                }
                catch (WebSocketException wsex)
                {
                    Console.WriteLine("[ERROR] WebSocket error: " + wsex.Message);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("[INFO] Server closed connection.");
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await HandleMessage(message);
            }
        }
        private async Task HandleMessage(string message)
        {
            // Twitch can send multiple IRC messages separated by \r\n, so split them
            var messages = message.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var msg in messages)
            {
                if (msg.StartsWith("PING"))
                {
                    // Reply PONG to keep connection alive
                    await Send(msg.Replace("PING", "PONG"));
                    continue;
                }
                if (msg.Contains("PRIVMSG"))
                {
                    var chatMessage = new IRCMessage(msg);
                    Console.WriteLine($"{chatMessage.Tags.GetValueOrDefault("display-name", "unknown")}: {chatMessage.Message}");
                    OnMessageRecieved.Invoke(this, chatMessage);
                    continue;
                }
                //Print other raw messages for info
                Console.WriteLine("[RAW] " + msg);
            }
        }

        private Task Send(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message + "\r\n");
            return _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
