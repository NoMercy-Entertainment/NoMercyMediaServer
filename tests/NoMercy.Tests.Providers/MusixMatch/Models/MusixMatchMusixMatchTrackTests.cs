using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Providers.MusixMatch.Models;

namespace NoMercy.Tests.Providers.MusixMatch.Models;

/// <summary>
/// PMOD-CRIT-01: Tests verifying that AlbumName is typed as string?, not long.
/// The bug: [JsonProperty("album_name")] public long AlbumName — album names are
/// strings (e.g. "Abbey Road"), not numbers. Deserialization of real API responses
/// would throw a JsonReaderException or silently produce 0.
/// The fix: Change to public string? AlbumName { get; set; }
/// </summary>
[Trait("Category", "Unit")]
public class MusixMatchMusixMatchTrackTests
{
    [Fact]
    public void AlbumName_PropertyType_IsNullableString()
    {
        PropertyInfo? property = typeof(MusixMatchMusixMatchTrack)
            .GetProperty(nameof(MusixMatchMusixMatchTrack.AlbumName));

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property.PropertyType);
    }

    [Fact]
    public void AlbumName_Deserializes_StringValue()
    {
        string json = """{"album_name": "Abbey Road"}""";

        MusixMatchMusixMatchTrack track = JsonConvert.DeserializeObject<MusixMatchMusixMatchTrack>(json)!;

        Assert.Equal("Abbey Road", track.AlbumName);
    }

    [Fact]
    public void AlbumName_Deserializes_NullValue()
    {
        string json = """{"album_name": null}""";

        MusixMatchMusixMatchTrack track = JsonConvert.DeserializeObject<MusixMatchMusixMatchTrack>(json)!;

        Assert.Null(track.AlbumName);
    }

    [Fact]
    public void AlbumName_Deserializes_EmptyString()
    {
        string json = """{"album_name": ""}""";

        MusixMatchMusixMatchTrack track = JsonConvert.DeserializeObject<MusixMatchMusixMatchTrack>(json)!;

        Assert.Equal("", track.AlbumName);
    }

    [Fact]
    public void AlbumName_DefaultValue_IsNull()
    {
        MusixMatchMusixMatchTrack track = new();

        Assert.Null(track.AlbumName);
    }

    [Fact]
    public void AlbumName_HasJsonPropertyAttribute_WithCorrectName()
    {
        PropertyInfo? property = typeof(MusixMatchMusixMatchTrack)
            .GetProperty(nameof(MusixMatchMusixMatchTrack.AlbumName));

        Assert.NotNull(property);

        JsonPropertyAttribute? attr = property.GetCustomAttribute<JsonPropertyAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("album_name", attr.PropertyName);
    }

    [Fact]
    public void AlbumName_RoundTrips_ThroughSerialization()
    {
        MusixMatchMusixMatchTrack track = new() { AlbumName = "The Dark Side of the Moon" };

        string json = JsonConvert.SerializeObject(track);
        MusixMatchMusixMatchTrack deserialized = JsonConvert.DeserializeObject<MusixMatchMusixMatchTrack>(json)!;

        Assert.Equal("The Dark Side of the Moon", deserialized.AlbumName);
    }
}
