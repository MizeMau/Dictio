using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dictio.Twitch
{
    internal class IRCMessage
    {
        public Dictionary<string, string> Tags { get; private set; } = new();
        public string Prefix { get; private set; } = "";
        public string Command { get; private set; } = "";
        public List<string> Parameters { get; private set; } = new();
        public string Message { get; private set; } = "";

        public IRCMessage(string rawMessage)
        {
            int pos = 0;
            int length = rawMessage.Length;

            // Parse tags if present (start with '@')
            if (pos < length && rawMessage[pos] == '@')
            {
                int endTags = rawMessage.IndexOf(' ');
                if (endTags == -1) endTags = length;
                var tagsPart = rawMessage.Substring(1, endTags - 1);
                Tags = ParseTags(tagsPart);
                pos = endTags + 1;
            }

            // Parse prefix if present (start with ':')
            if (pos < length && rawMessage[pos] == ':')
            {
                int endPrefix = rawMessage.IndexOf(' ', pos);
                if (endPrefix == -1) endPrefix = length;
                Prefix = rawMessage.Substring(pos + 1, endPrefix - pos - 1);
                pos = endPrefix + 1;
            }

            // Parse command
            int nextSpace = rawMessage.IndexOf(' ', pos);
            if (nextSpace == -1) nextSpace = length;
            Command = rawMessage.Substring(pos, nextSpace - pos);
            pos = nextSpace + 1;

            // Parse parameters
            while (pos < length)
            {
                if (rawMessage[pos] == ':')
                {
                    // Rest is the message parameter
                    Message = rawMessage.Substring(pos + 1);
                    break;
                }

                int nextParamEnd = rawMessage.IndexOf(' ', pos);
                if (nextParamEnd == -1) nextParamEnd = length;
                var param = rawMessage.Substring(pos, nextParamEnd - pos);
                Parameters.Add(param);
                pos = nextParamEnd + 1;
            }
        }

        private Dictionary<string, string> ParseTags(string tagsPart)
        {
            var tags = new Dictionary<string, string>();
            var tagPairs = tagsPart.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var tagPair in tagPairs)
            {
                var kv = tagPair.Split('=', 2);
                var key = kv[0];
                var value = kv.Length > 1 ? kv[1] : "";
                tags[key] = value;
            }
            return tags;
        }
    }
}
