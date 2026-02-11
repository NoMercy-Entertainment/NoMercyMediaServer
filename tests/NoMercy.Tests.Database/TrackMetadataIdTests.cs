using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Tests.Database;

[Trait("Category", "Characterization")]
public class TrackMetadataIdTests
{
    [Fact]
    public void MetadataId_PropertyType_IsNullableUlid()
    {
        PropertyInfo? prop = typeof(Track).GetProperty("MetadataId");
        Assert.NotNull(prop);
        Assert.Equal(typeof(Ulid?), prop.PropertyType);
    }

    [Fact]
    public void MetadataId_MatchesMetadataIdType()
    {
        Type trackMetadataIdType = typeof(Track).GetProperty("MetadataId")!.PropertyType;
        Type metadataIdType = typeof(Metadata).GetProperty("Id")!.PropertyType;

        Type trackFkUnderlyingType = Nullable.GetUnderlyingType(trackMetadataIdType) ?? trackMetadataIdType;
        Assert.Equal(metadataIdType, trackFkUnderlyingType);
    }

    [Fact]
    public void MetadataId_ConsistentWithVideoFileMetadataId()
    {
        Type trackType = typeof(Track).GetProperty("MetadataId")!.PropertyType;
        Type videoFileType = typeof(VideoFile).GetProperty("MetadataId")!.PropertyType;
        Assert.Equal(videoFileType, trackType);
    }

    [Fact]
    public void MetadataId_ConsistentWithAlbumMetadataId()
    {
        Type trackType = typeof(Track).GetProperty("MetadataId")!.PropertyType;
        Type albumType = typeof(Album).GetProperty("MetadataId")!.PropertyType;
        Assert.Equal(albumType, trackType);
    }

    [Fact]
    public void MetadataId_HasCorrectJsonProperty()
    {
        PropertyInfo? prop = typeof(Track).GetProperty("MetadataId");
        JsonPropertyAttribute? attr = prop?.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("metadata_id", attr.PropertyName);
    }

    [Fact]
    public void MetadataId_DefaultValue_IsNull()
    {
        Track track = new();
        Assert.Null(track.MetadataId);
    }

    [Fact]
    public void MetadataId_CanBeAssignedUlid()
    {
        Ulid testId = Ulid.NewUlid();
        Track track = new() { MetadataId = testId };
        Assert.Equal(testId, track.MetadataId);
    }

    [Fact]
    public void MetadataId_CanBeAssignedNull()
    {
        Track track = new() { MetadataId = Ulid.NewUlid() };
        track.MetadataId = null;
        Assert.Null(track.MetadataId);
    }

    [Fact]
    public void MetadataId_SerializesToJson()
    {
        Ulid testId = Ulid.NewUlid();
        Track track = new() { MetadataId = testId };
        string json = JsonConvert.SerializeObject(track);
        Assert.Contains("\"metadata_id\"", json);
        Assert.Contains(testId.ToString(), json);
    }

    [Fact]
    public void MetadataId_IsNotInt()
    {
        PropertyInfo? prop = typeof(Track).GetProperty("MetadataId");
        Assert.NotNull(prop);
        Assert.NotEqual(typeof(int?), prop.PropertyType);
        Assert.NotEqual(typeof(int), prop.PropertyType);
    }

    [Fact]
    public void Metadata_NavigationProperty_Exists()
    {
        PropertyInfo? prop = typeof(Track).GetProperty("Metadata");
        Assert.NotNull(prop);
        Assert.Equal(typeof(Metadata), prop.PropertyType);
    }
}
