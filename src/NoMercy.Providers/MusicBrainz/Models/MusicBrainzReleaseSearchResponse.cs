using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzReleaseSearchResponse
{
    [JsonProperty("created")]
    public DateTimeOffset Created { get; set; }

    [JsonProperty("count")]
    public long Count { get; set; }

    [JsonProperty("offset")]
    public long Offset { get; set; }

    [JsonProperty("releases")] public MusicBrainzRelease[] Releases { get; set; } = [];
}