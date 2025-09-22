using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dictio.Twitch.Events
{
    public class TwitchRaid
    {
        public string EventType => "TwitchRaid";

        [JsonPropertyName("from_broadcaster_user_id")]
        public string FromBroadcasterUserID { get; set; }

        [JsonPropertyName("from_broadcaster_user_login")]
        public string FromBroadcasterUserLogin { get; set; }

        [JsonPropertyName("from_broadcaster_user_name")]
        public string FromBroadcasterUserName { get; set; }

        [JsonPropertyName("to_broadcaster_user_id")]
        public string ToBroadcasterUserID { get; set; }

        [JsonPropertyName("to_broadcaster_user_login")]
        public string ToBroadcasterUserLogin { get; set; }

        [JsonPropertyName("to_broadcaster_user_name")]
        public string ToBroadcasterUserName { get; set; }

        [JsonPropertyName("viewers")]
        public long Viewers { get; set; }
    }
}
