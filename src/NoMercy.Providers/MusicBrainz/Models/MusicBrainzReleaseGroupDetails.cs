using Newtonsoft.Json;

namespace NoMercy.Providers.MusicBrainz.Models;
public class MusicBrainzReleaseGroupDetails : MusicBrainzReleaseGroup
{
    [JsonProperty("releases")] public MusicBrainzRelease[] Releases { get; set; } = [];
    [JsonProperty("relations")] public MusicBrainzWorkRelation[] Relations { get; set; } = [];
}