using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Tests.Database;

[Trait("Category", "Characterization")]
public class LibraryJsonPropertyTests
{
    private static string? GetJsonPropertyName(string propertyName)
    {
        PropertyInfo? prop = typeof(Library).GetProperty(propertyName);
        Assert.NotNull(prop);
        JsonPropertyAttribute? attr = prop.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(attr);
        return attr.PropertyName;
    }

    [Fact]
    public void ChapterImages_JsonProperty_IsChapterImages()
    {
        string? name = GetJsonPropertyName("ChapterImages");
        Assert.Equal("chapter_images", name);
    }

    [Fact]
    public void ExtractChapters_JsonProperty_IsExtractChapters()
    {
        string? name = GetJsonPropertyName("ExtractChapters");
        Assert.Equal("extract_chapters", name);
    }

    [Fact]
    public void ExtractChaptersDuring_JsonProperty_IsExtractChaptersDuring()
    {
        string? name = GetJsonPropertyName("ExtractChaptersDuring");
        Assert.Equal("extract_chapters_during", name);
    }

    [Fact]
    public void AutoRefreshInterval_JsonProperty_IsAutoRefreshInterval()
    {
        string? name = GetJsonPropertyName("AutoRefreshInterval");
        Assert.Equal("auto_refresh_interval", name);
    }

    [Fact]
    public void Image_JsonProperty_IsImage()
    {
        string? name = GetJsonPropertyName("Image");
        Assert.Equal("image", name);
    }

    [Fact]
    public void Order_JsonProperty_IsOrder()
    {
        string? name = GetJsonPropertyName("Order");
        Assert.Equal("order", name);
    }

    [Fact]
    public void Title_JsonProperty_IsTitle()
    {
        string? name = GetJsonPropertyName("Title");
        Assert.Equal("title", name);
    }

    [Fact]
    public void Type_JsonProperty_IsType()
    {
        string? name = GetJsonPropertyName("Type");
        Assert.Equal("type", name);
    }

    [Fact]
    public void Id_JsonProperty_IsId()
    {
        string? name = GetJsonPropertyName("Id");
        Assert.Equal("id", name);
    }

    [Fact]
    public void Serialization_ChapterImages_UsesCorrectJsonKey()
    {
        Library library = new() { ChapterImages = true };
        string json = JsonConvert.SerializeObject(library);
        Assert.Contains("\"chapter_images\":true", json);
        Assert.DoesNotContain("\"auto_refresh_interval\":true", json);
    }

    [Fact]
    public void Serialization_AutoRefreshInterval_UsesCorrectJsonKey()
    {
        Library library = new() { AutoRefreshInterval = 30 };
        string json = JsonConvert.SerializeObject(library);
        Assert.Contains("\"auto_refresh_interval\":30", json);
        Assert.DoesNotContain("\"name\":30", json);
    }

    [Fact]
    public void Serialization_ExtractChaptersDuring_UsesCorrectJsonKey()
    {
        Library library = new() { ExtractChaptersDuring = true };
        string json = JsonConvert.SerializeObject(library);
        Assert.Contains("\"extract_chapters_during\":true", json);
        Assert.DoesNotContain("\"extract_chapters\":true", json);
    }

    [Fact]
    public void Deserialization_ChapterImages_FromCorrectJsonKey()
    {
        string json = """{"chapter_images": true}""";
        Library? library = JsonConvert.DeserializeObject<Library>(json);
        Assert.NotNull(library);
        Assert.True(library.ChapterImages);
    }

    [Fact]
    public void Deserialization_AutoRefreshInterval_FromCorrectJsonKey()
    {
        string json = """{"auto_refresh_interval": 45}""";
        Library? library = JsonConvert.DeserializeObject<Library>(json);
        Assert.NotNull(library);
        Assert.Equal(45, library.AutoRefreshInterval);
    }

    [Fact]
    public void Deserialization_ExtractChaptersDuring_FromCorrectJsonKey()
    {
        string json = """{"extract_chapters_during": true}""";
        Library? library = JsonConvert.DeserializeObject<Library>(json);
        Assert.NotNull(library);
        Assert.True(library.ExtractChaptersDuring);
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
            Image = "/test.png"
        };

        string json = JsonConvert.SerializeObject(original);
        Library? deserialized = JsonConvert.DeserializeObject<Library>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.ChapterImages, deserialized.ChapterImages);
        Assert.Equal(original.ExtractChapters, deserialized.ExtractChapters);
        Assert.Equal(original.ExtractChaptersDuring, deserialized.ExtractChaptersDuring);
        Assert.Equal(original.AutoRefreshInterval, deserialized.AutoRefreshInterval);
        Assert.Equal(original.Image, deserialized.Image);
    }

    [Theory]
    [InlineData("ChapterImages", "chapter_images")]
    [InlineData("ExtractChapters", "extract_chapters")]
    [InlineData("ExtractChaptersDuring", "extract_chapters_during")]
    [InlineData("AutoRefreshInterval", "auto_refresh_interval")]
    [InlineData("Image", "image")]
    [InlineData("Order", "order")]
    [InlineData("PerfectSubtitleMatch", "perfect_subtitle_match")]
    [InlineData("Realtime", "realtime")]
    [InlineData("SpecialSeasonName", "special_season_name")]
    [InlineData("Title", "title")]
    [InlineData("Type", "type")]
    public void JsonPropertyName_MatchesSnakeCaseOfPropertyName(string propertyName, string expectedJsonName)
    {
        string? actualJsonName = GetJsonPropertyName(propertyName);
        Assert.Equal(expectedJsonName, actualJsonName);
    }
}
