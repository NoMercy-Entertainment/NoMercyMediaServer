using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Database.Models;

namespace NoMercy.Tests.Database;

[Trait("Category", "Characterization")]
public class UserDataJsonPropertyTests
{
    private static string? GetJsonPropertyName(string propertyName)
    {
        PropertyInfo? prop = typeof(UserData).GetProperty(propertyName);
        Assert.NotNull(prop);
        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(attr);
        return attr.PropertyName;
    }

    [Fact]
    public void TvId_JsonProperty_IsTvId()
    {
        string? name = GetJsonPropertyName("TvId");
        Assert.Equal("tv_id", name);
    }

    [Fact]
    public void TvId_JsonProperty_IsNotEpisodeId()
    {
        string? name = GetJsonPropertyName("TvId");
        Assert.NotEqual("episode_id", name);
    }

    [Fact]
    public void MovieId_JsonProperty_IsMovieId()
    {
        string? name = GetJsonPropertyName("MovieId");
        Assert.Equal("movie_id", name);
    }

    [Fact]
    public void CollectionId_JsonProperty_IsCollectionId()
    {
        string? name = GetJsonPropertyName("CollectionId");
        Assert.Equal("collection_id", name);
    }

    [Fact]
    public void SpecialId_JsonProperty_IsSpecialId()
    {
        string? name = GetJsonPropertyName("SpecialId");
        Assert.Equal("special_id", name);
    }

    [Fact]
    public void UserId_JsonProperty_IsUserId()
    {
        string? name = GetJsonPropertyName("UserId");
        Assert.Equal("user_id", name);
    }

    [Fact]
    public void VideoFileId_JsonProperty_IsVideoFileId()
    {
        string? name = GetJsonPropertyName("VideoFileId");
        Assert.Equal("video_file_id", name);
    }

    [Fact]
    public void TvId_Serializes_AsTvId()
    {
        UserData userData = new()
        {
            TvId = 42
        };

        string json = JsonConvert.SerializeObject(userData);

        Assert.Contains("\"tv_id\":42", json);
        Assert.DoesNotContain("\"episode_id\"", json);
    }

    [Fact]
    public void TvId_Deserializes_FromTvIdKey()
    {
        string json = "{\"tv_id\": 99}";

        UserData? userData = JsonConvert.DeserializeObject<UserData>(json);

        Assert.NotNull(userData);
        Assert.Equal(99, userData.TvId);
    }

    [Fact]
    public void TvId_DoesNotDeserialize_FromEpisodeIdKey()
    {
        string json = "{\"episode_id\": 99}";

        UserData? userData = JsonConvert.DeserializeObject<UserData>(json);

        Assert.NotNull(userData);
        Assert.Null(userData.TvId);
    }

    [Fact]
    public void TvId_RoundTrip_PreservesValue()
    {
        UserData original = new()
        {
            TvId = 123
        };

        string json = JsonConvert.SerializeObject(original);
        UserData? deserialized = JsonConvert.DeserializeObject<UserData>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.TvId, deserialized.TvId);
    }

    [Theory]
    [InlineData("Id", "id")]
    [InlineData("Rating", "name")]
    [InlineData("LastPlayedDate", "last_played_date")]
    [InlineData("Audio", "audio")]
    [InlineData("Subtitle", "subtitle")]
    [InlineData("SubtitleType", "subtitle_type")]
    [InlineData("Time", "time")]
    [InlineData("Type", "type")]
    [InlineData("UserId", "user_id")]
    [InlineData("MovieId", "movie_id")]
    [InlineData("TvId", "tv_id")]
    [InlineData("CollectionId", "collection_id")]
    [InlineData("SpecialId", "special_id")]
    [InlineData("VideoFileId", "video_file_id")]
    public void AllFkProperties_HaveCorrectJsonProperty(string propertyName, string expectedJsonName)
    {
        string? name = GetJsonPropertyName(propertyName);
        Assert.Equal(expectedJsonName, name);
    }
}
