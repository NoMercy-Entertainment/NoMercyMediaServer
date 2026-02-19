using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Providers.TVDB.Models;

namespace NoMercy.Tests.Providers.TVDB.Models;

/// <summary>
/// PMOD-CRIT-02: Tests verifying that TvdbAwardCategoryData.ForSeries and
/// TvdbCharacterTagOption.Tag have setters, so JSON deserialization can
/// populate them. Without setters, Newtonsoft.Json silently skips the values.
/// </summary>
[Trait("Category", "Unit")]
public class TvdbModelPropertyTests
{
    [Fact]
    public void ForSeries_HasSetter()
    {
        PropertyInfo? prop = typeof(TvdbAwardCategoryData)
            .GetProperty(nameof(TvdbAwardCategoryData.ForSeries));

        Assert.NotNull(prop);
        Assert.True(prop.CanWrite,
            "TvdbAwardCategoryData.ForSeries must have a setter for JSON deserialization");
    }

    [Fact]
    public void ForSeries_DeserializesTrue()
    {
        string json = """{"forSeries": true}""";
        TvdbAwardCategoryData? result = JsonConvert.DeserializeObject<TvdbAwardCategoryData>(json);

        Assert.NotNull(result);
        Assert.True(result.ForSeries);
    }

    [Fact]
    public void ForSeries_DeserializesFalse()
    {
        string json = """{"forSeries": false}""";
        TvdbAwardCategoryData? result = JsonConvert.DeserializeObject<TvdbAwardCategoryData>(json);

        Assert.NotNull(result);
        Assert.False(result.ForSeries);
    }

    [Fact]
    public void ForSeries_RoundTrip()
    {
        TvdbAwardCategoryData original = new() { ForSeries = true };
        string json = JsonConvert.SerializeObject(original);
        TvdbAwardCategoryData? deserialized = JsonConvert.DeserializeObject<TvdbAwardCategoryData>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.ForSeries, deserialized.ForSeries);
    }

    [Fact]
    public void ForSeries_JsonPropertyAttribute()
    {
        PropertyInfo? prop = typeof(TvdbAwardCategoryData)
            .GetProperty(nameof(TvdbAwardCategoryData.ForSeries));

        Assert.NotNull(prop);

        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("forSeries", attr.PropertyName);
    }

    [Fact]
    public void Tag_HasSetter()
    {
        PropertyInfo? prop = typeof(TvdbCharacterTagOption)
            .GetProperty(nameof(TvdbCharacterTagOption.Tag));

        Assert.NotNull(prop);
        Assert.True(prop.CanWrite,
            "TvdbCharacterTagOption.Tag must have a setter for JSON deserialization");
    }

    [Fact]
    public void Tag_DeserializesValue()
    {
        string json = """{"tag": 42}""";
        TvdbCharacterTagOption? result = JsonConvert.DeserializeObject<TvdbCharacterTagOption>(json);

        Assert.NotNull(result);
        Assert.Equal(42, result.Tag);
    }

    [Fact]
    public void Tag_DeserializesZero()
    {
        string json = """{"tag": 0}""";
        TvdbCharacterTagOption? result = JsonConvert.DeserializeObject<TvdbCharacterTagOption>(json);

        Assert.NotNull(result);
        Assert.Equal(0, result.Tag);
    }

    [Fact]
    public void Tag_RoundTrip()
    {
        TvdbCharacterTagOption original = new() { Tag = 99 };
        string json = JsonConvert.SerializeObject(original);
        TvdbCharacterTagOption? deserialized = JsonConvert.DeserializeObject<TvdbCharacterTagOption>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Tag, deserialized.Tag);
    }

    [Fact]
    public void Tag_JsonPropertyAttribute()
    {
        PropertyInfo? prop = typeof(TvdbCharacterTagOption)
            .GetProperty(nameof(TvdbCharacterTagOption.Tag));

        Assert.NotNull(prop);

        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("tag", attr.PropertyName);
    }

    [Fact]
    public void Tag_DefaultValue()
    {
        TvdbCharacterTagOption instance = new();
        Assert.Equal(0, instance.Tag);
    }

    [Fact]
    public void ForSeries_DefaultValue()
    {
        TvdbAwardCategoryData instance = new();
        Assert.False(instance.ForSeries);
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

        TvdbAwardCategoryData? result = JsonConvert.DeserializeObject<TvdbAwardCategoryData>(json);

        Assert.NotNull(result);
        Assert.True(result.AllowCoNominees);
        Assert.False(result.ForMovies);
        Assert.True(result.ForSeries);
        Assert.Equal(5, result.Id);
        Assert.Equal("Best Drama", result.Name);
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

        TvdbCharacterTagOption? result = JsonConvert.DeserializeObject<TvdbCharacterTagOption>(json);

        Assert.NotNull(result);
        Assert.Equal("Some help", result.HelpText);
        Assert.Equal(10, result.Id);
        Assert.Equal("Action", result.Name);
        Assert.Equal(7, result.Tag);
        Assert.Equal("Genre", result.TagName);
    }
}
