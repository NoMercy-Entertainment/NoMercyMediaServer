using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzReleaseAppends : MusicBrainzRelease
{
    // [JsonProperty("aliases")] public object[] Aliases { get; set; }
    // [JsonProperty("annotation")] public object Annotation { get; set; }

    // [JsonProperty("asin")] public object Asin { get; set; }
    [JsonProperty("collections")] public Collection[] Collections { get; set; } = [];
    [JsonProperty("cover-art-archive")] public CoverArtArchive CoverArtArchive { get; set; } = new();
    [JsonProperty("label-info")] public LabelInfo[] LabelInfo { get; set; } = [];
    [JsonProperty("relations")] public MusicBrainzWorkRelation[] Relations { get; set; } = [];
    [JsonProperty("tags")] public MusicBrainzTag[] Tags { get; set; } = [];
}