using System.Reflection;
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
public class CastNavigationInitializerTests
{
    [Fact]
    public void Movie_Navigation_DefaultIsNull()
    {
        Cast cast = new();
        Assert.Null(cast.Movie);
    }

    [Fact]
    public void Tv_Navigation_DefaultIsNull()
    {
        Cast cast = new();
        Assert.Null(cast.Tv);
    }

    [Fact]
    public void Season_Navigation_DefaultIsNull()
    {
        Cast cast = new();
        Assert.Null(cast.Season);
    }

    [Fact]
    public void Episode_Navigation_DefaultIsNull()
    {
        Cast cast = new();
        Assert.Null(cast.Episode);
    }

    [Fact]
    public void Movie_Navigation_IsNullable()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty("Movie");
        Assert.NotNull(prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(prop);
        Assert.Equal(NullabilityState.Nullable, info.ReadState);
    }

    [Fact]
    public void Tv_Navigation_IsNullable()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty("Tv");
        Assert.NotNull(prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(prop);
        Assert.Equal(NullabilityState.Nullable, info.ReadState);
    }

    [Fact]
    public void Season_Navigation_IsNullable()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty("Season");
        Assert.NotNull(prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(prop);
        Assert.Equal(NullabilityState.Nullable, info.ReadState);
    }

    [Fact]
    public void Episode_Navigation_IsNullable()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty("Episode");
        Assert.NotNull(prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(prop);
        Assert.Equal(NullabilityState.Nullable, info.ReadState);
    }

    [Fact]
    public void NullCheck_Movie_WorksCorrectly_WhenNotLoaded()
    {
        Cast cast = new();
        bool hasMovie = cast.Movie is not null;
        Assert.False(hasMovie);
    }

    [Fact]
    public void NullCheck_Tv_WorksCorrectly_WhenNotLoaded()
    {
        Cast cast = new();
        bool hasTv = cast.Tv is not null;
        Assert.False(hasTv);
    }

    [Fact]
    public void NullCheck_Season_WorksCorrectly_WhenNotLoaded()
    {
        Cast cast = new();
        bool hasSeason = cast.Season is not null;
        Assert.False(hasSeason);
    }

    [Fact]
    public void NullCheck_Episode_WorksCorrectly_WhenNotLoaded()
    {
        Cast cast = new();
        bool hasEpisode = cast.Episode is not null;
        Assert.False(hasEpisode);
    }

    [Theory]
    [InlineData("Movie")]
    [InlineData("Tv")]
    [InlineData("Season")]
    [InlineData("Episode")]
    public void NullableNavigation_HasNoFieldInitializer_ToNew(string propertyName)
    {
        Cast cast = new();
        PropertyInfo? prop = typeof(Cast).GetProperty(propertyName);
        Assert.NotNull(prop);
        object? value = prop.GetValue(cast);
        Assert.Null(value);
    }

    [Fact]
    public void Person_Navigation_IsNotNull_WithInitializer()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty("Person");
        Assert.NotNull(prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(prop);
        Assert.Equal(NullabilityState.NotNull, info.ReadState);
    }

    [Fact]
    public void Role_Navigation_IsNotNull_WithInitializer()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty("Role");
        Assert.NotNull(prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(prop);
        Assert.Equal(NullabilityState.NotNull, info.ReadState);
    }

    [Fact]
    public void Movie_CanBeAssigned()
    {
        Movie movie = new() { Id = 1 };
        Cast cast = new() { Movie = movie, MovieId = 1 };
        Assert.NotNull(cast.Movie);
        Assert.Equal(1, cast.Movie.Id);
    }

    [Fact]
    public void Movie_CanBeSetToNull()
    {
        Movie movie = new() { Id = 1 };
        Cast cast = new() { Movie = movie, MovieId = 1 };
        cast.Movie = null;
        Assert.Null(cast.Movie);
    }
}
