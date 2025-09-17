using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dictio.Twitch.Events
{
    public class TwitchFollower
    {
        public string EventType => "TwitchFollower";

        [JsonPropertyName("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonPropertyName("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonPropertyName("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }

        [JsonPropertyName("user_id")]
        public string UserID { get; set; }

        [JsonPropertyName("user_login")]
        public string UserLogin { get; set; }

        [JsonPropertyName("user_name")]
        public string UserName { get; set; }

        [JsonPropertyName("followed_at")]
        public string FollowedAt { get; set; }
    }
}
