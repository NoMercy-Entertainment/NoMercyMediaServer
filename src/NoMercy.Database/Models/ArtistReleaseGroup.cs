using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(ArtistId), nameof(ReleaseGroupId))]
[Index(nameof(ArtistId))]
[Index(nameof(ReleaseGroupId))]
public class ArtistReleaseGroup
{
    [JsonProperty("artist_id")] public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    [JsonProperty("release_id")] public Guid ReleaseGroupId { get; set; }
    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public ArtistReleaseGroup()
    {
    }

    public ArtistReleaseGroup(Guid albumId, Guid releaseId)
    {
        ArtistId = albumId;
        ReleaseGroupId = releaseId;
    }
}
