using Newtonsoft.Json;

namespace NoMercy.Database.Models.Libraries;

public sealed class CandidateMatch
{
    [JsonProperty("provider")]
    public required string Provider { get; set; }

    [JsonProperty("external_id")]
    public required string ExternalId { get; set; }

    [JsonProperty("title")]
    public required string Title { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }

    [JsonProperty("poster_path")]
    public string? PosterPath { get; set; }

    [JsonProperty("score")]
    public double Score { get; set; }
}
