using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(AlbumId), nameof(ReleaseGroupId))]
[Index(nameof(AlbumId))]
[Index(nameof(ReleaseGroupId))]
public class AlbumReleaseGroup
{
    [JsonProperty("album_id")] public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    [JsonProperty("release_id")] public Guid ReleaseGroupId { get; set; }
    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public AlbumReleaseGroup()
    {
    }

    public AlbumReleaseGroup(Guid albumId, Guid releaseId)
    {
        AlbumId = albumId;
        ReleaseGroupId = releaseId;
    }
}
