using Dictio.Twitch;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace Dictio.Aspectus
{
    internal class WebSocket
    {
        private TcpListener _server;
        private Dictionary<Guid, NetworkStream> _streams = new();

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
                TcpClient client = await _server.AcceptTcpClientAsync();
                Console.WriteLine("A client connected.");
                _ = Task.Run(() => ReaderWorker(client));
            }
        }

        // Helper method to check if client is connected
        private bool IsClientConnected(TcpClient client)
        {
            try
            {
                if (client == null || !client.Connected)
                    return false;

                // More reliable connection check
                if (client.Client.Poll(0, SelectMode.SelectRead))
                {
                    byte[] buff = new byte[1];
                    if (client.Client.Receive(buff, SocketFlags.Peek) == 0)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        private Task ReaderWorker(TcpClient client)
        {
            Guid id = Guid.NewGuid();
            _streams.Add(id, client.GetStream());
            while (IsClientConnected(client))
            {
                if (!_streams[id].DataAvailable || client.Available < 3) continue;

                byte[] bytes = new byte[client.Available];
                _streams[id].Read(bytes, 0, bytes.Length);
                string message = Encoding.UTF8.GetString(bytes);

                if (Regex.IsMatch(message, "^GET", RegexOptions.IgnoreCase))
                {
                    HandleConnectMessage(message, id);
                    continue;
                }
                HandleMessage(message, bytes, id);
            }
            _streams[id].Dispose();
            return Task.CompletedTask;
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

            SendRaw(response, id);  // <- raw, not framed!
        }

        private void HandleMessage(string message, byte[] bytes, Guid id)
        {
            bool fin = (bytes[0] & 0b10000000) != 0;
            bool mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
            ulong offset = 2;
            ulong msgLen = bytes[1] & (ulong)0b01111111;

            if (!mask) return;
            switch (msgLen)
            {
                case 0:
                    return;
                case 126:
                    msgLen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                    offset = 4;
                    break;
                case 127:
                    msgLen = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0);
                    offset = 10;
                    break;
            }

            byte[] decoded = new byte[msgLen];
            byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
            offset += 4;

            for (ulong i = 0; i < msgLen; ++i)
                decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

            string text = Encoding.UTF8.GetString(decoded);
            var test = new IRCMessage(text, true);
            SendMessage(test, id);
        }

        #endregion

        #region Send Message
        private void SendRaw(string text, Guid id)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            _streams[id].Write(bytes, 0, bytes.Length);
        }

        public void SendMessage(IRCMessage ircMessage, Guid? id = null)
        {
            string message = JsonConvert.SerializeObject(ircMessage);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] frame = CreateWebSocketFrame(messageBytes);
            if (id.HasValue)
            {
                _streams[id.Value].Write(frame, 0, frame.Length);
                return;
            }
            foreach (var stream in _streams)
            {
                stream.Value.Write(frame, 0, frame.Length);
            }
        }

        private byte[] CreateWebSocketFrame(byte[] payload)
        {
            int headerLength = CalculateHeaderLength(payload.Length);
            byte[] frame = new byte[headerLength + payload.Length];

            WriteFrameHeader(frame, payload.Length);
            Buffer.BlockCopy(payload, 0, frame, headerLength, payload.Length);

            return frame;
        }

        private int CalculateHeaderLength(int payloadLength)
        {
            if (payloadLength <= 125) 
                return 2;
            if (payloadLength <= 65535) 
                return 4;
            return 10;
        }

        private void WriteFrameHeader(byte[] frame, int payloadLength)
        {
            // FIN bit set, text frame
            frame[0] = 0x81;

            if (payloadLength <= 125)
            {
                frame[1] = (byte)payloadLength;
            }
            else if (payloadLength <= 65535)
            {
                frame[1] = 126;
                frame[2] = (byte)((payloadLength >> 8) & 0xFF);
                frame[3] = (byte)(payloadLength & 0xFF);
            }
            else
            {
                frame[1] = 127;
                for (int i = 0; i < 8; i++)
                {
                    frame[9 - i] = (byte)((payloadLength >> (8 * i)) & 0xFF);
                }
            }
        }
        #endregion
    }
}
