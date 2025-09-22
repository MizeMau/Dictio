using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dictio.Twitch.Events
{
    public class TwitchChatMessageDelete
    {
        public string EventType => "TwitchChatMessageDelete";

        [JsonPropertyName("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonPropertyName("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonPropertyName("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }

        [JsonPropertyName("target_user_id")]
        public string TargetUserId { get; set; }

        [JsonPropertyName("target_user_name")]
        public string TargetUserLogin { get; set; }

        [JsonPropertyName("target_user_login")]
        public string TargetUserName { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }
    }
}
