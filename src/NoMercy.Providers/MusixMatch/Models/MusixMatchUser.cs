using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class MusixMatchUser
{
    [JsonProperty("uaid")] public string Uaid { get; set; } = string.Empty;
    [JsonProperty("is_mine")] public int IsMine { get; set; }
    [JsonProperty("user_name")] public string UserName { get; set; } = string.Empty;
    [JsonProperty("user_profile_photo")] public string UserProfilePhoto { get; set; } = string.Empty;
    [JsonProperty("has_private_profile")] public int HasPrivateProfile { get; set; }
    [JsonProperty("score")] public int Score { get; set; }
    [JsonProperty("position")] public int Position { get; set; }
    [JsonProperty("weekly_score")] public int WeeklyScore { get; set; }
    [JsonProperty("level")] public string Level { get; set; } = string.Empty;
    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("rank_level")] public int RankLevel { get; set; }
    [JsonProperty("points_to_next_level")] public int PointsToNextLevel { get; set; }
    [JsonProperty("ratio_to_next_level")] public double RatioToNextLevel { get; set; }
    [JsonProperty("rank_name")] public string RankName { get; set; } = string.Empty;
    [JsonProperty("next_rank_name")] public string NextRankName { get; set; } = string.Empty;
    [JsonProperty("ratio_to_next_rank")] public double RatioToNextRank { get; set; }
    [JsonProperty("rank_color")] public string RankColor { get; set; } = string.Empty;
    [JsonProperty("rank_colors")] public MusixMatchRankColors MusixMatchRankColors { get; set; } = new();
    [JsonProperty("rank_image_url")] public string RankImageUrl { get; set; } = string.Empty;
    [JsonProperty("next_rank_color")] public string NextRankColor { get; set; } = string.Empty;
    [JsonProperty("next_rank_colors")] public MusixMatchRankColors NextMusixMatchRankColors { get; set; } = new();
    [JsonProperty("next_rank_image_url")] public string NextRankImageUrl { get; set; } = string.Empty;
    [JsonProperty("counters")] public MusixMatchCounters MusixMatchCounters { get; set; } = new();
    [JsonProperty("academy_completed")] public bool AcademyCompleted { get; set; }
    [JsonProperty("moderator")] public bool Moderator { get; set; }
}