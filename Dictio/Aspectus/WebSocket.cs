using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dictio.Aspectus
{
    internal class WebSocket
    {
        private TcpListener _server;
        private NetworkStream _stream;

        public Task CliendWorkerTask;
        internal WebSocket(string ip = "127.0.0.1", int port = 9656)
        {
            _server = new TcpListener(IPAddress.Parse(ip), port);
            _server.Start();
            Console.WriteLine($"Server has started on {ip}:{port}, Waiting for a connection…");
            CliendWorkerTask = Task.Run(CliendWorker);
        }

        private async Task CliendWorker()
        {
            while (true)
            {
                TcpClient client = _server.AcceptTcpClient();
                Console.WriteLine("A client connected.");
                await ReaderWorker(client);
            }
        }

        private Task ReaderWorker(TcpClient client)
        {
            _stream = client.GetStream();
            while (client.Connected)
            {
                while (!_stream.DataAvailable) ;
                while (client.Available < 3) ; // match against "get"

                byte[] bytes = new byte[client.Available];
                _stream.Read(bytes, 0, bytes.Length);
                string s = Encoding.UTF8.GetString(bytes);


                if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
                {
                    string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                    string swkAndSalt = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    byte[] swkAndSaltSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swkAndSalt));
                    string swkAndSaltSha1Base64 = Convert.ToBase64String(swkAndSaltSha1);

                    // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                    string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                        "Connection: Upgrade\r\n" +
                        "Upgrade: websocket\r\n" +
                        $"Sec-WebSocket-Accept: {swkAndSaltSha1Base64}\r\n\r\n";

                    SendMessage(response);
                }
            }
            return Task.CompletedTask;
        }
        /// <summary>
        /// this method needs to be refactored
        /// </summary>
        /// <param name="message"></param>
        private void SendMessage(string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            // WebSocket frame format: [FIN|RSV|OPCODE] [MASK|Payload Length] [Extended Payload Length] [Payload Data]
            byte[] frame;

            if (messageBytes.Length <= 125)
            {
                frame = new byte[2 + messageBytes.Length];
                frame[0] = 0x81; // FIN bit set, text frame
                frame[1] = (byte)messageBytes.Length; // No mask, payload length
                Array.Copy(messageBytes, 0, frame, 2, messageBytes.Length);
            }
            else if (messageBytes.Length <= 65535)
            {
                frame = new byte[4 + messageBytes.Length];
                frame[0] = 0x81; // FIN bit set, text frame
                frame[1] = 126; // 126 indicates extended payload length (16-bit)
                frame[2] = (byte)((messageBytes.Length >> 8) & 0xFF); // Length high byte
                frame[3] = (byte)(messageBytes.Length & 0xFF); // Length low byte
                Array.Copy(messageBytes, 0, frame, 4, messageBytes.Length);
            }
            else
            {
                frame = new byte[10 + messageBytes.Length];
                frame[0] = 0x81; // FIN bit set, text frame
                frame[1] = 127; // 127 indicates extended payload length (64-bit)
                // Write 64-bit length (big-endian)
                for (int i = 0; i < 8; i++)
                {
                    frame[9 - i] = (byte)((messageBytes.Length >> (8 * i)) & 0xFF);
                }
                Array.Copy(messageBytes, 0, frame, 10, messageBytes.Length);
            }

            _stream.Write(frame, 0, frame.Length);
        }

        [Obsolete]
        public void test()
        {
            string ip = "127.0.0.1";
            int port = 80;
            var server = new TcpListener(IPAddress.Parse(ip), port);

            server.Start();
            Console.WriteLine("Server has started on {0}:{1}, Waiting for a connection…", ip, port);

            TcpClient client = server.AcceptTcpClient();
            Console.WriteLine("A client connected.");

            NetworkStream stream = client.GetStream();
            // enter to an infinite cycle to be able to handle every change in stream
            while (true)
            {
                while (!stream.DataAvailable) ;
                while (client.Available < 3) ; // match against "get"

                byte[] bytes = new byte[client.Available];
                stream.Read(bytes, 0, bytes.Length);
                string s = Encoding.UTF8.GetString(bytes);

                if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
                {
                    Console.WriteLine("=====Handshaking from client=====\n{0}", s);

                    // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                    // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                    // 3. Compute SHA-1 and Base64 hash of the new value
                    // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                    string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                    string swkAndSalt = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                    byte[] swkAndSaltSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swkAndSalt));
                    string swkAndSaltSha1Base64 = Convert.ToBase64String(swkAndSaltSha1);

                    // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                    byte[] response = Encoding.UTF8.GetBytes(
                        "HTTP/1.1 101 Switching Protocols\r\n" +
                        "Connection: Upgrade\r\n" +
                        "Upgrade: websocket\r\n" +
                        "Sec-WebSocket-Accept: " + swkAndSaltSha1Base64 + "\r\n\r\n");

                    stream.Write(response, 0, response.Length);
                }
                else
                {
                    bool fin = (bytes[0] & 0b10000000) != 0,
                        mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
                    int opcode = bytes[0] & 0b00001111; // expecting 1 - text message
                    ulong offset = 2,
                          msgLen = bytes[1] & (ulong)0b01111111;

                    if (msgLen == 126)
                    {
                        // bytes are reversed because websocket will print them in Big-Endian, whereas
                        // BitConverter will want them arranged in little-endian on windows
                        msgLen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                        offset = 4;
                    }
                    else if (msgLen == 127)
                    {
                        // To test the below code, we need to manually buffer larger messages — since the NIC's autobuffering
                        // may be too latency-friendly for this code to run (that is, we may have only some of the bytes in this
                        // websocket frame available through client.Available).
                        msgLen = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0);
                        offset = 10;
                    }

                    if (msgLen == 0)
                    {
                        Console.WriteLine("msgLen == 0");
                    }
                    else if (mask)
                    {
                        byte[] decoded = new byte[msgLen];
                        byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                        offset += 4;

                        for (ulong i = 0; i < msgLen; ++i)
                            decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                        string text = Encoding.UTF8.GetString(decoded);
                        Console.WriteLine("{0}", text);

                        // Echo the message back to the client
                        SendMessage(stream, text);
                    }
                    else
                        Console.WriteLine("mask bit not set");

                    Console.WriteLine();
                }
            }
        }
        [Obsolete]
        private void SendMessage(NetworkStream stream, string message)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);

            // WebSocket frame format: [FIN|RSV|OPCODE] [MASK|Payload Length] [Extended Payload Length] [Payload Data]
            byte[] frame;

            if (messageBytes.Length <= 125)
            {
                frame = new byte[2 + messageBytes.Length];
                frame[0] = 0x81; // FIN bit set, text frame
                frame[1] = (byte)messageBytes.Length; // No mask, payload length
                Array.Copy(messageBytes, 0, frame, 2, messageBytes.Length);
            }
            else if (messageBytes.Length <= 65535)
            {
                frame = new byte[4 + messageBytes.Length];
                frame[0] = 0x81; // FIN bit set, text frame
                frame[1] = 126; // 126 indicates extended payload length (16-bit)
                frame[2] = (byte)((messageBytes.Length >> 8) & 0xFF); // Length high byte
                frame[3] = (byte)(messageBytes.Length & 0xFF); // Length low byte
                Array.Copy(messageBytes, 0, frame, 4, messageBytes.Length);
            }
            else
            {
                frame = new byte[10 + messageBytes.Length];
                frame[0] = 0x81; // FIN bit set, text frame
                frame[1] = 127; // 127 indicates extended payload length (64-bit)
                // Write 64-bit length (big-endian)
                for (int i = 0; i < 8; i++)
                {
                    frame[9 - i] = (byte)((messageBytes.Length >> (8 * i)) & 0xFF);
                }
                Array.Copy(messageBytes, 0, frame, 10, messageBytes.Length);
            }

            stream.Write(frame, 0, frame.Length);
            Console.WriteLine("Echoed: {0}", message);
        }
    }
}
