using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dictio.Twitch
{
    public class TwitchChatMessage
    {
        [JsonPropertyName("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonPropertyName("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonPropertyName("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }

        [JsonPropertyName("source_broadcaster_user_id")]
        public string SourceBroadcasterUserId { get; set; }

        [JsonPropertyName("source_broadcaster_user_login")]
        public string SourceBroadcasterUserLogin { get; set; }

        [JsonPropertyName("source_broadcaster_user_name")]
        public string SourceBroadcasterUserName { get; set; }

        [JsonPropertyName("chatter_user_id")]
        public string ChatterUserId { get; set; }

        [JsonPropertyName("chatter_user_login")]
        public string ChatterUserLogin { get; set; }

        [JsonPropertyName("chatter_user_name")]
        public string ChatterUserName { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("source_message_id")]
        public string SourceMessageId { get; set; }

        [JsonPropertyName("is_source_only")]
        public bool? IsSourceOnly { get; set; }

        [JsonPropertyName("message")]
        public ChatMessage Message { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("badges")]
        public List<Badge> Badges { get; set; }

        [JsonPropertyName("source_badges")]
        public object SourceBadges { get; set; }

        [JsonPropertyName("message_type")]
        public string MessageType { get; set; }

        [JsonPropertyName("cheer")]
        public object Cheer { get; set; }

        [JsonPropertyName("reply")]
        public object Reply { get; set; }

        [JsonPropertyName("channel_points_custom_reward_id")]
        public string ChannelPointsCustomRewardId { get; set; }

        [JsonPropertyName("channel_points_animation_id")]
        public string ChannelPointsAnimationId { get; set; }
    }
    public class ChatMessage
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("fragments")]
        public List<Fragment> Fragments { get; set; }
    }

    public class Fragment
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("cheermote")]
        public object Cheermote { get; set; }

        [JsonPropertyName("emote")]
        public object Emote { get; set; }

        [JsonPropertyName("mention")]
        public object Mention { get; set; }
    }

    public class Badge
    {
        [JsonPropertyName("set_id")]
        public string SetId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("info")]
        public string Info { get; set; }
    }
}
