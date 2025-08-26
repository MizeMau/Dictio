using Dictio.Twitch;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dictio.Websites
{
    internal class WebSocket
    {
        private readonly TcpListener _server;
        private readonly ConcurrentDictionary<Guid, NetworkStream> _streams = new();

        public Task ClientWorkerTask;

        internal WebSocket(string ip = "127.0.0.1", int port = 9656)
        {
            _server = new TcpListener(IPAddress.Parse(ip), port);
            _server.Start();
            Console.WriteLine($"Server has started on {ip}:{port}, Waiting for a connection…");
            ClientWorkerTask = Task.Run(ClientWorker);
        }

        private async Task ClientWorker()
        {
            while (true)
            {
                var client = await _server.AcceptTcpClientAsync();
                Console.WriteLine("A client connected.");
                _ = Task.Run(() => ReaderWorker(client));
            }
        }

        private async Task ReaderWorker(TcpClient client)
        {
            Guid id = Guid.NewGuid();
            using var stream = client.GetStream();
            _streams.TryAdd(id, stream);
            
            var buffer = new byte[8192]; // 4096
            try
            {
                while (client.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0) break; // disconnected

                    string header = Encoding.UTF8.GetString(buffer, 0, read);

                    if (Regex.IsMatch(header, "^GET", RegexOptions.IgnoreCase))
                    {
                        HandleConnectMessage(header, id);
                        continue;
                    }

                    HandleMessage(buffer, read, id);
                }
            }
            catch (IOException)
            {
                // connection dropped
            }
            finally
            {
                _streams.TryRemove(id, out _);
                client.Close();
            }
        }

        #region Handle Messages
        private void HandleConnectMessage(string message, Guid id)
        {
            string swk = Regex.Match(message, @"Sec-WebSocket-Key:\s*(.+)").Groups[1].Value.Trim();

            string swkAndSalt = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] swkAndSaltSha1 = System.Security.Cryptography.SHA1.Create()
                .ComputeHash(Encoding.UTF8.GetBytes(swkAndSalt));
            string swkAndSaltSha1Base64 = Convert.ToBase64String(swkAndSaltSha1);

            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {swkAndSaltSha1Base64}\r\n" +
                "\r\n";

            SendRaw(response, id);  // raw, no frame
        }

        private void HandleMessage(byte[] buffer, int read, Guid id)
        {
            bool fin = (buffer[0] & 0b10000000) != 0;
            int opcode = buffer[0] & 0b00001111;
            bool mask = (buffer[1] & 0b10000000) != 0;
            ulong msgLen = (ulong)(buffer[1] & 0b01111111);

            int offset = 2;

            if (msgLen == 126)
            {
                msgLen = (ulong)(buffer[2] << 8 | buffer[3]);
                offset = 4;
            }
            else if (msgLen == 127)
            {
                msgLen = BitConverter.ToUInt64(buffer.Skip(2).Take(8).Reverse().ToArray(), 0);
                offset = 10;
            }

            if (!mask) return;

            byte[] masks = new byte[4] { buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3] };
            offset += 4;

            byte[] decoded = new byte[msgLen];
            for (ulong i = 0; i < msgLen; i++)
                decoded[i] = (byte)(buffer[offset + (int)i] ^ masks[i % 4]);

            string text = Encoding.UTF8.GetString(decoded);
            var ircMessage = new IRCMessage(text, true);
            SendMessage(ircMessage, id);
        }
        #endregion

        #region Send Message
        private void SendRaw(string text, Guid id)
        {
            if (_streams.TryGetValue(id, out var stream))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        public void SendMessage(IRCMessage ircMessage, Guid? id = null)
        {
            string message = JsonConvert.SerializeObject(ircMessage);
            byte[] payload = Encoding.UTF8.GetBytes(message);
            byte[] frame = CreateWebSocketFrame(payload);

            if (id.HasValue)
            {
                if (_streams.TryGetValue(id.Value, out var stream))
                    stream.Write(frame, 0, frame.Length);
            }
            else
            {
                foreach (var stream in _streams.Values)
                    stream.Write(frame, 0, frame.Length);
            }
        }

        private byte[] CreateWebSocketFrame(byte[] payload)
        {
            List<byte> frame = new();
            frame.Add(0x81); // FIN + text frame

            if (payload.Length <= 125)
            {
                frame.Add((byte)payload.Length);
            }
            else if (payload.Length <= 65535)
            {
                frame.Add(126);
                frame.Add((byte)((payload.Length >> 8) & 0xFF));
                frame.Add((byte)(payload.Length & 0xFF));
            }
            else
            {
                frame.Add(127);
                for (int i = 7; i >= 0; i--)
                    frame.Add((byte)((payload.Length >> (8 * i)) & 0xFF));
            }

            frame.AddRange(payload);
            return frame.ToArray();
        }
        #endregion
    }
}
