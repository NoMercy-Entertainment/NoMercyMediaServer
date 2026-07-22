// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Database.Models.Users;

namespace NoMercy.Tests.Database;

[Trait(name: "Category", value: "Characterization")]
public class UserDataJsonPropertyTests
{
    private static string? GetJsonPropertyName(string propertyName)
    {
        PropertyInfo? prop = typeof(UserData).GetProperty(name: propertyName);
        Assert.NotNull(@object: prop);
        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(@object: attr);
        return attr.PropertyName;
    }

    [Fact]
    public void TvId_JsonProperty_IsTvId()
    {
        string? name = GetJsonPropertyName(propertyName: "TvId");
        Assert.Equal(expected: "tv_id", actual: name);
    }

    [Fact]
    public void TvId_JsonProperty_IsNotEpisodeId()
    {
        string? name = GetJsonPropertyName(propertyName: "TvId");
        Assert.NotEqual(expected: "episode_id", actual: name);
    }

    [Fact]
    public void MovieId_JsonProperty_IsMovieId()
    {
        string? name = GetJsonPropertyName(propertyName: "MovieId");
        Assert.Equal(expected: "movie_id", actual: name);
    }

    [Fact]
    public void CollectionId_JsonProperty_IsCollectionId()
    {
        string? name = GetJsonPropertyName(propertyName: "CollectionId");
        Assert.Equal(expected: "collection_id", actual: name);
    }

    [Fact]
    public void SpecialId_JsonProperty_IsSpecialId()
    {
        string? name = GetJsonPropertyName(propertyName: "SpecialId");
        Assert.Equal(expected: "special_id", actual: name);
    }

    [Fact]
    public void UserId_JsonProperty_IsUserId()
    {
        string? name = GetJsonPropertyName(propertyName: "UserId");
        Assert.Equal(expected: "user_id", actual: name);
    }

    [Fact]
    public void VideoFileId_JsonProperty_IsVideoFileId()
    {
        string? name = GetJsonPropertyName(propertyName: "VideoFileId");
        Assert.Equal(expected: "video_file_id", actual: name);
    }

    [Fact]
    public void TvId_Serializes_AsTvId()
    {
        UserData userData = new() { TvId = 42 };

        string json = JsonConvert.SerializeObject(value: userData);

        Assert.Contains(expectedSubstring: "\"tv_id\":42", actualString: json);
        Assert.DoesNotContain(expectedSubstring: "\"episode_id\"", actualString: json);
    }

    [Fact]
    public void TvId_Deserializes_FromTvIdKey()
    {
        string json = "{\"tv_id\": 99}";

        UserData? userData = JsonConvert.DeserializeObject<UserData>(value: json);

        Assert.NotNull(@object: userData);
        Assert.Equal(expected: 99, actual: userData.TvId);
    }

    [Fact]
    public void TvId_DoesNotDeserialize_FromEpisodeIdKey()
    {
        string json = "{\"episode_id\": 99}";

        UserData? userData = JsonConvert.DeserializeObject<UserData>(value: json);

        Assert.NotNull(@object: userData);
        Assert.Null(value: userData.TvId);
    }

    [Fact]
    public void TvId_RoundTrip_PreservesValue()
    {
        UserData original = new() { TvId = 123 };

        string json = JsonConvert.SerializeObject(value: original);
        UserData? deserialized = JsonConvert.DeserializeObject<UserData>(value: json);

        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: original.TvId, actual: deserialized.TvId);
    }

    [Theory]
    [InlineData(data: ["Id", "id"])]
    [InlineData(data: ["Rating", "rating"])]
    [InlineData(data: ["LastPlayedDate", "last_played_date"])]
    [InlineData(data: ["Audio", "audio"])]
    [InlineData(data: ["Subtitle", "subtitle"])]
    [InlineData(data: ["SubtitleType", "subtitle_type"])]
    [InlineData(data: ["Time", "time"])]
    [InlineData(data: ["Type", "type"])]
    [InlineData(data: ["UserId", "user_id"])]
    [InlineData(data: ["MovieId", "movie_id"])]
    [InlineData(data: ["TvId", "tv_id"])]
    [InlineData(data: ["CollectionId", "collection_id"])]
    [InlineData(data: ["SpecialId", "special_id"])]
    [InlineData(data: ["VideoFileId", "video_file_id"])]
    public void AllFkProperties_HaveCorrectJsonProperty(
        string propertyName,
        string expectedJsonName
    )
    {
        string? name = GetJsonPropertyName(propertyName: propertyName);
        Assert.Equal(expected: expectedJsonName, actual: name);
    }
}
