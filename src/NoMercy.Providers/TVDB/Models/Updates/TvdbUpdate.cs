using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Providers.TVDB.Models.Updates;

public class TvdbUpdatesResponse : TvdbResponse<TvdbUpdate[]> { }

public class TvdbUpdate
{
    [JsonProperty("recordType")]
    public string RecordType { get; set; } = string.Empty;

    [JsonProperty("recordId")]
    public long RecordId { get; set; }

    [JsonProperty("methodInt")]
    public int MethodInt { get; set; }

    [JsonProperty("method")]
    public string Method { get; set; } = string.Empty;

    [JsonProperty("extraInfo")]
    public string? ExtraInfo { get; set; }

    [JsonProperty("userId")]
    public long? UserId { get; set; }

    [JsonProperty("timeStamp")]
    public long TimeStamp { get; set; }

    [JsonProperty("entityType")]
    public string? EntityType { get; set; }

    [JsonProperty("mergeToId")]
    public long? MergeToId { get; set; }

    [JsonProperty("mergeToEntityType")]
    public string? MergeToEntityType { get; set; }
}
