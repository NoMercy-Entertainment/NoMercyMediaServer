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
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Tests.Database;

[Trait(name: "Category", value: "Characterization")]
public class LibraryJsonPropertyTests
{
    private static string? GetJsonPropertyName(string propertyName)
    {
        PropertyInfo? prop = typeof(Library).GetProperty(name: propertyName);
        Assert.NotNull(@object: prop);
        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(@object: attr);
        return attr.PropertyName;
    }

    [Fact]
    public void ChapterImages_JsonProperty_IsChapterImages()
    {
        string? name = GetJsonPropertyName(propertyName: "ChapterImages");
        Assert.Equal(expected: "chapter_images", actual: name);
    }

    [Fact]
    public void ExtractChapters_JsonProperty_IsExtractChapters()
    {
        string? name = GetJsonPropertyName(propertyName: "ExtractChapters");
        Assert.Equal(expected: "extract_chapters", actual: name);
    }

    [Fact]
    public void ExtractChaptersDuring_JsonProperty_IsExtractChaptersDuring()
    {
        string? name = GetJsonPropertyName(propertyName: "ExtractChaptersDuring");
        Assert.Equal(expected: "extract_chapters_during", actual: name);
    }

    [Fact]
    public void AutoRefreshInterval_JsonProperty_IsAutoRefreshInterval()
    {
        string? name = GetJsonPropertyName(propertyName: "AutoRefreshInterval");
        Assert.Equal(expected: "auto_refresh_interval", actual: name);
    }

    [Fact]
    public void Image_JsonProperty_IsImage()
    {
        string? name = GetJsonPropertyName(propertyName: "Image");
        Assert.Equal(expected: "image", actual: name);
    }

    [Fact]
    public void Order_JsonProperty_IsOrder()
    {
        string? name = GetJsonPropertyName(propertyName: "Order");
        Assert.Equal(expected: "order", actual: name);
    }

    [Fact]
    public void Title_JsonProperty_IsTitle()
    {
        string? name = GetJsonPropertyName(propertyName: "Title");
        Assert.Equal(expected: "title", actual: name);
    }

    [Fact]
    public void Type_JsonProperty_IsType()
    {
        string? name = GetJsonPropertyName(propertyName: "Type");
        Assert.Equal(expected: "type", actual: name);
    }

    [Fact]
    public void Id_JsonProperty_IsId()
    {
        string? name = GetJsonPropertyName(propertyName: "Id");
        Assert.Equal(expected: "id", actual: name);
    }

    [Fact]
    public void Serialization_ChapterImages_UsesCorrectJsonKey()
    {
        Library library = new() { ChapterImages = true };
        string json = JsonConvert.SerializeObject(value: library);
        Assert.Contains(expectedSubstring: "\"chapter_images\":true", actualString: json);
        Assert.DoesNotContain(expectedSubstring: "\"auto_refresh_interval\":true", actualString: json);
    }

    [Fact]
    public void Serialization_AutoRefreshInterval_UsesCorrectJsonKey()
    {
        Library library = new() { AutoRefreshInterval = 30 };
        string json = JsonConvert.SerializeObject(value: library);
        Assert.Contains(expectedSubstring: "\"auto_refresh_interval\":30", actualString: json);
        Assert.DoesNotContain(expectedSubstring: "\"name\":30", actualString: json);
    }

    [Fact]
    public void Serialization_ExtractChaptersDuring_UsesCorrectJsonKey()
    {
        Library library = new() { ExtractChaptersDuring = true };
        string json = JsonConvert.SerializeObject(value: library);
        Assert.Contains(expectedSubstring: "\"extract_chapters_during\":true", actualString: json);
        Assert.DoesNotContain(expectedSubstring: "\"extract_chapters\":true", actualString: json);
    }

    [Fact]
    public void Deserialization_ChapterImages_FromCorrectJsonKey()
    {
        string json = """{"chapter_images": true}""";
        Library? library = JsonConvert.DeserializeObject<Library>(value: json);
        Assert.NotNull(@object: library);
        Assert.True(condition: library.ChapterImages);
    }

    [Fact]
    public void Deserialization_AutoRefreshInterval_FromCorrectJsonKey()
    {
        string json = """{"auto_refresh_interval": 45}""";
        Library? library = JsonConvert.DeserializeObject<Library>(value: json);
        Assert.NotNull(@object: library);
        Assert.Equal(expected: 45, actual: library.AutoRefreshInterval);
    }

    [Fact]
    public void Deserialization_ExtractChaptersDuring_FromCorrectJsonKey()
    {
        string json = """{"extract_chapters_during": true}""";
        Library? library = JsonConvert.DeserializeObject<Library>(value: json);
        Assert.NotNull(@object: library);
        Assert.True(condition: library.ExtractChaptersDuring);
    }

    [Fact]
    public void RoundTrip_AllShiftedProperties_PreserveValues()
    {
        Library original = new()
        {
            ChapterImages = true,
            ExtractChapters = true,
            ExtractChaptersDuring = false,
            AutoRefreshInterval = 60,
            Image = "/test.png",
        };

        string json = JsonConvert.SerializeObject(value: original);
        Library? deserialized = JsonConvert.DeserializeObject<Library>(value: json);

        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: original.ChapterImages, actual: deserialized.ChapterImages);
        Assert.Equal(expected: original.ExtractChapters, actual: deserialized.ExtractChapters);
        Assert.Equal(expected: original.ExtractChaptersDuring, actual: deserialized.ExtractChaptersDuring);
        Assert.Equal(expected: original.AutoRefreshInterval, actual: deserialized.AutoRefreshInterval);
        Assert.Equal(expected: original.Image, actual: deserialized.Image);
    }

    [Theory]
    [InlineData(data: ["ChapterImages", "chapter_images"])]
    [InlineData(data: ["ExtractChapters", "extract_chapters"])]
    [InlineData(data: ["ExtractChaptersDuring", "extract_chapters_during"])]
    [InlineData(data: ["AutoRefreshInterval", "auto_refresh_interval"])]
    [InlineData(data: ["Image", "image"])]
    [InlineData(data: ["Order", "order"])]
    [InlineData(data: ["PerfectSubtitleMatch", "perfect_subtitle_match"])]
    [InlineData(data: ["Realtime", "realtime"])]
    [InlineData(data: ["SpecialSeasonName", "special_season_name"])]
    [InlineData(data: ["Title", "title"])]
    [InlineData(data: ["Type", "type"])]
    public void JsonPropertyName_MatchesSnakeCaseOfPropertyName(
        string propertyName,
        string expectedJsonName
    )
    {
        string? actualJsonName = GetJsonPropertyName(propertyName: propertyName);
        Assert.Equal(expected: expectedJsonName, actual: actualJsonName);
    }
}
