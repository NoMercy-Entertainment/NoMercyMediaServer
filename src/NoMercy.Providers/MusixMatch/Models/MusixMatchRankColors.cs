using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;
public class MusixMatchRankColors
{
    [JsonProperty("rank_color_10")] public string RankColor10 { get; set; } = string.Empty;
    [JsonProperty("rank_color_50")] public string RankColor50 { get; set; } = string.Empty;
    [JsonProperty("rank_color_100")] public string RankColor100 { get; set; } = string.Empty;
    [JsonProperty("rank_color_200")] public string RankColor200 { get; set; } = string.Empty;
}