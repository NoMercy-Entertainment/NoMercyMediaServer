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
using NoMercy.Providers.TVDB.Models.Awards;
using NoMercy.Providers.TVDB.Models.Shared;

namespace NoMercy.Tests.Providers.TVDB.Models;

/// <summary>
/// PMOD-CRIT-02: Tests verifying that TvdbAwardCategory.ForSeries and
/// TvdbTagOption.Tag have setters, so JSON deserialization can
/// populate them. Without setters, Newtonsoft.Json silently skips the values.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class TvdbModelPropertyTests
{
    [Fact]
    public void ForSeries_HasSetter()
    {
        PropertyInfo? prop = typeof(TvdbAwardCategory).GetProperty(
            name: nameof(TvdbAwardCategory.ForSeries)
        );

        Assert.NotNull(@object: prop);
        Assert.True(
            condition: prop.CanWrite,
            userMessage: "TvdbAwardCategory.ForSeries must have a setter for JSON deserialization"
        );
    }

    [Fact]
    public void ForSeries_DeserializesTrue()
    {
        string json = """{"forSeries": true}""";
        TvdbAwardCategory? result = JsonConvert.DeserializeObject<TvdbAwardCategory>(value: json);

        Assert.NotNull(@object: result);
        Assert.True(condition: result.ForSeries);
    }

    [Fact]
    public void ForSeries_DeserializesFalse()
    {
        string json = """{"forSeries": false}""";
        TvdbAwardCategory? result = JsonConvert.DeserializeObject<TvdbAwardCategory>(value: json);

        Assert.NotNull(@object: result);
        Assert.False(condition: result.ForSeries);
    }

    [Fact]
    public void ForSeries_RoundTrip()
    {
        TvdbAwardCategory original = new() { ForSeries = true };
        string json = JsonConvert.SerializeObject(value: original);
        TvdbAwardCategory? deserialized = JsonConvert.DeserializeObject<TvdbAwardCategory>(value: json);

        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: original.ForSeries, actual: deserialized.ForSeries);
    }

    [Fact]
    public void ForSeries_JsonPropertyAttribute()
    {
        PropertyInfo? prop = typeof(TvdbAwardCategory).GetProperty(
            name: nameof(TvdbAwardCategory.ForSeries)
        );

        Assert.NotNull(@object: prop);

        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(@object: attr);
        Assert.Equal(expected: "forSeries", actual: attr.PropertyName);
    }

    [Fact]
    public void Tag_HasSetter()
    {
        PropertyInfo? prop = typeof(TvdbTagOption).GetProperty(name: nameof(TvdbTagOption.Tag));

        Assert.NotNull(@object: prop);
        Assert.True(condition: prop.CanWrite, userMessage: "TvdbTagOption.Tag must have a setter for JSON deserialization");
    }

    [Fact]
    public void Tag_DeserializesValue()
    {
        string json = """{"tag": 42}""";
        TvdbTagOption? result = JsonConvert.DeserializeObject<TvdbTagOption>(value: json);

        Assert.NotNull(@object: result);
        Assert.Equal(expected: 42, actual: result.Tag);
    }

    [Fact]
    public void Tag_DeserializesZero()
    {
        string json = """{"tag": 0}""";
        TvdbTagOption? result = JsonConvert.DeserializeObject<TvdbTagOption>(value: json);

        Assert.NotNull(@object: result);
        Assert.Equal(expected: 0, actual: result.Tag);
    }

    [Fact]
    public void Tag_RoundTrip()
    {
        TvdbTagOption original = new() { Tag = 99 };
        string json = JsonConvert.SerializeObject(value: original);
        TvdbTagOption? deserialized = JsonConvert.DeserializeObject<TvdbTagOption>(value: json);

        Assert.NotNull(@object: deserialized);
        Assert.Equal(expected: original.Tag, actual: deserialized.Tag);
    }

    [Fact]
    public void Tag_JsonPropertyAttribute()
    {
        PropertyInfo? prop = typeof(TvdbTagOption).GetProperty(name: nameof(TvdbTagOption.Tag));

        Assert.NotNull(@object: prop);

        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(@object: attr);
        Assert.Equal(expected: "tag", actual: attr.PropertyName);
    }

    [Fact]
    public void Tag_DefaultValue()
    {
        TvdbTagOption instance = new();
        Assert.Equal(expected: 0, actual: instance.Tag);
    }

    [Fact]
    public void ForSeries_DefaultValue()
    {
        TvdbAwardCategory instance = new();
        Assert.False(condition: instance.ForSeries);
    }

    [Fact]
    public void FullAwardCategoryDeserialization()
    {
        string json = """
            {
                "allowCoNominees": true,
                "forMovies": false,
                "forSeries": true,
                "id": 5,
                "name": "Best Drama"
            }
            """;

        TvdbAwardCategory? result = JsonConvert.DeserializeObject<TvdbAwardCategory>(value: json);

        Assert.NotNull(@object: result);
        Assert.True(condition: result.AllowCoNominees);
        Assert.False(condition: result.ForMovies);
        Assert.True(condition: result.ForSeries);
        Assert.Equal(expected: 5, actual: result.Id);
        Assert.Equal(expected: "Best Drama", actual: result.Name);
    }

    [Fact]
    public void FullCharacterTagOptionDeserialization()
    {
        string json = """
            {
                "helpText": "Some help",
                "id": 10,
                "name": "Action",
                "tag": 7,
                "tagName": "Genre"
            }
            """;

        TvdbTagOption? result = JsonConvert.DeserializeObject<TvdbTagOption>(value: json);

        Assert.NotNull(@object: result);
        Assert.Equal(expected: "Some help", actual: result.HelpText);
        Assert.Equal(expected: 10, actual: result.Id);
        Assert.Equal(expected: "Action", actual: result.Name);
        Assert.Equal(expected: 7, actual: result.Tag);
        Assert.Equal(expected: "Genre", actual: result.TagName);
    }
}
