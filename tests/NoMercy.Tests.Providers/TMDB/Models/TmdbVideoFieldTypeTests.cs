using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Providers.TMDB.Models.Collections;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;

namespace NoMercy.Tests.Providers.TMDB.Models;

[Trait("Category", "Characterization")]
public class TmdbVideoFieldTypeTests
{
    // --- TmdbMovie ---

    [Fact]
    public void TmdbMovie_Video_PropertyType_IsBoolNullable()
    {
        PropertyInfo? prop = typeof(TmdbMovie).GetProperty("Video");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(bool?));
    }

    [Fact]
    public void TmdbMovie_Video_HasJsonPropertyAttribute()
    {
        PropertyInfo? prop = typeof(TmdbMovie).GetProperty("Video");
        JsonPropertyAttribute? attr = prop!.GetCustomAttribute<JsonPropertyAttribute>();
        attr.Should().NotBeNull();
        attr!.PropertyName.Should().Be("video");
    }

    [Fact]
    public void TmdbMovie_Video_DeserializesTrue()
    {
        string json = """{"video": true}""";
        TmdbMovie? movie = JsonConvert.DeserializeObject<TmdbMovie>(json);
        movie.Should().NotBeNull();
        movie!.Video.Should().BeTrue();
    }

    [Fact]
    public void TmdbMovie_Video_DeserializesFalse()
    {
        string json = """{"video": false}""";
        TmdbMovie? movie = JsonConvert.DeserializeObject<TmdbMovie>(json);
        movie.Should().NotBeNull();
        movie!.Video.Should().BeFalse();
    }

    [Fact]
    public void TmdbMovie_Video_DeserializesNull()
    {
        string json = """{"video": null}""";
        TmdbMovie? movie = JsonConvert.DeserializeObject<TmdbMovie>(json);
        movie.Should().NotBeNull();
        movie!.Video.Should().BeNull();
    }

    [Fact]
    public void TmdbMovie_Video_DefaultIsNull()
    {
        TmdbMovie movie = new();
        movie.Video.Should().BeNull();
    }

    [Fact]
    public void TmdbMovie_Video_RoundTrip()
    {
        TmdbMovie original = new() { Video = true };
        string json = JsonConvert.SerializeObject(original);
        TmdbMovie? deserialized = JsonConvert.DeserializeObject<TmdbMovie>(json);
        deserialized!.Video.Should().Be(original.Video);
    }

    // --- TmdbCollectionPart ---

    [Fact]
    public void TmdbCollectionPart_Video_PropertyType_IsBoolNullable()
    {
        PropertyInfo? prop = typeof(TmdbCollectionPart).GetProperty("Video");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(bool?));
    }

    [Fact]
    public void TmdbCollectionPart_Video_HasJsonPropertyAttribute()
    {
        PropertyInfo? prop = typeof(TmdbCollectionPart).GetProperty("Video");
        JsonPropertyAttribute? attr = prop!.GetCustomAttribute<JsonPropertyAttribute>();
        attr.Should().NotBeNull();
        attr!.PropertyName.Should().Be("video");
    }

    [Fact]
    public void TmdbCollectionPart_Video_DeserializesTrue()
    {
        string json = """{"video": true}""";
        TmdbCollectionPart? part = JsonConvert.DeserializeObject<TmdbCollectionPart>(json);
        part.Should().NotBeNull();
        part!.Video.Should().BeTrue();
    }

    [Fact]
    public void TmdbCollectionPart_Video_DeserializesFalse()
    {
        string json = """{"video": false}""";
        TmdbCollectionPart? part = JsonConvert.DeserializeObject<TmdbCollectionPart>(json);
        part.Should().NotBeNull();
        part!.Video.Should().BeFalse();
    }

    [Fact]
    public void TmdbCollectionPart_Video_DeserializesNull()
    {
        string json = """{"video": null}""";
        TmdbCollectionPart? part = JsonConvert.DeserializeObject<TmdbCollectionPart>(json);
        part.Should().NotBeNull();
        part!.Video.Should().BeNull();
    }

    [Fact]
    public void TmdbCollectionPart_Video_RoundTrip()
    {
        TmdbCollectionPart original = new() { Video = false };
        string json = JsonConvert.SerializeObject(original);
        TmdbCollectionPart? deserialized = JsonConvert.DeserializeObject<TmdbCollectionPart>(json);
        deserialized!.Video.Should().Be(original.Video);
    }

    // --- TmdbShowOrMovie ---

    [Fact]
    public void TmdbShowOrMovie_Video_PropertyType_IsBoolNullable()
    {
        PropertyInfo? prop = typeof(TmdbShowOrMovie).GetProperty("Video");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(bool?));
    }

    [Fact]
    public void TmdbShowOrMovie_Video_HasJsonPropertyAttribute()
    {
        PropertyInfo? prop = typeof(TmdbShowOrMovie).GetProperty("Video");
        JsonPropertyAttribute? attr = prop!.GetCustomAttribute<JsonPropertyAttribute>();
        attr.Should().NotBeNull();
        attr!.PropertyName.Should().Be("video");
    }

    [Fact]
    public void TmdbShowOrMovie_Video_CopiedFromTmdbMovie()
    {
        TmdbMovie movie = new() { Video = true };
        TmdbShowOrMovie showOrMovie = new(movie);
        showOrMovie.Video.Should().Be(movie.Video);
    }

    [Fact]
    public void TmdbShowOrMovie_Video_CopiedFromTmdbMovie_False()
    {
        TmdbMovie movie = new() { Video = false };
        TmdbShowOrMovie showOrMovie = new(movie);
        showOrMovie.Video.Should().BeFalse();
    }

    [Fact]
    public void TmdbShowOrMovie_Video_CopiedFromTmdbMovie_Null()
    {
        TmdbMovie movie = new() { Video = null };
        TmdbShowOrMovie showOrMovie = new(movie);
        showOrMovie.Video.Should().BeNull();
    }

    // --- Real TMDB API JSON deserialization ---

    [Fact]
    public void TmdbMovie_Video_DeserializesFromRealisticApiJson()
    {
        string json = """
            {
                "adult": false,
                "backdrop_path": "/qhQnY2fUcQqOomvJWgUHDrjNPzO.jpg",
                "id": 155,
                "original_language": "en",
                "original_title": "The Dark Knight",
                "overview": "Batman raises the stakes.",
                "popularity": 123.456,
                "poster_path": "/qJ2tW6WMUDux911r6m7haRef0WH.jpg",
                "release_date": "2008-07-16",
                "title": "The Dark Knight",
                "video": false,
                "vote_average": 9.0,
                "vote_count": 32000
            }
            """;
        TmdbMovie? movie = JsonConvert.DeserializeObject<TmdbMovie>(json);
        movie.Should().NotBeNull();
        movie!.Video.Should().BeFalse();
        movie.Id.Should().Be(155);
        movie.Title.Should().Be("The Dark Knight");
    }

    [Fact]
    public void TmdbCollectionPart_Video_DeserializesFromRealisticApiJson()
    {
        string json = """
            {
                "adult": false,
                "backdrop_path": "/path.jpg",
                "genre_ids": [28, 80, 18],
                "id": 155,
                "original_language": "en",
                "original_title": "The Dark Knight",
                "overview": "Batman raises the stakes.",
                "release_date": "2008-07-16",
                "poster_path": "/poster.jpg",
                "popularity": 123.456,
                "title": "The Dark Knight",
                "video": false,
                "vote_average": 9.0,
                "vote_count": 32000
            }
            """;
        TmdbCollectionPart? part = JsonConvert.DeserializeObject<TmdbCollectionPart>(json);
        part.Should().NotBeNull();
        part!.Video.Should().BeFalse();
        part.Id.Should().Be(155);
    }

    [Fact]
    public void TmdbShowOrMovie_Video_PreservedFromTmdbMovieDeserialization()
    {
        string json = """
            {
                "adult": false,
                "id": 155,
                "original_language": "en",
                "original_title": "The Dark Knight",
                "overview": "Batman raises the stakes.",
                "popularity": 123.456,
                "poster_path": "/poster.jpg",
                "release_date": "2008-07-16",
                "title": "The Dark Knight",
                "video": false,
                "vote_average": 9.0,
                "vote_count": 32000
            }
            """;
        TmdbMovie? movie = JsonConvert.DeserializeObject<TmdbMovie>(json);
        movie.Should().NotBeNull();
        TmdbShowOrMovie showOrMovie = new(movie!);
        showOrMovie.Video.Should().BeFalse();
    }
}
