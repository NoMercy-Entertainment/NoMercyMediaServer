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
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.People;

namespace NoMercy.Tests.Database;

[Trait(name: "Category", value: "Characterization")]
public class CastNavigationInitializerTests
{
    [Fact]
    public void Movie_Navigation_DefaultIsNull()
    {
        Cast cast = new();
        Assert.Null(@object: cast.Movie);
    }

    [Fact]
    public void Tv_Navigation_DefaultIsNull()
    {
        Cast cast = new();
        Assert.Null(@object: cast.Tv);
    }

    [Fact]
    public void Season_Navigation_DefaultIsNull()
    {
        Cast cast = new();
        Assert.Null(@object: cast.Season);
    }

    [Fact]
    public void Episode_Navigation_DefaultIsNull()
    {
        Cast cast = new();
        Assert.Null(@object: cast.Episode);
    }

    [Fact]
    public void Movie_Navigation_IsNullable()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty(name: "Movie");
        Assert.NotNull(@object: prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(propertyInfo: prop);
        Assert.Equal(expected: NullabilityState.Nullable, actual: info.ReadState);
    }

    [Fact]
    public void Tv_Navigation_IsNullable()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty(name: "Tv");
        Assert.NotNull(@object: prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(propertyInfo: prop);
        Assert.Equal(expected: NullabilityState.Nullable, actual: info.ReadState);
    }

    [Fact]
    public void Season_Navigation_IsNullable()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty(name: "Season");
        Assert.NotNull(@object: prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(propertyInfo: prop);
        Assert.Equal(expected: NullabilityState.Nullable, actual: info.ReadState);
    }

    [Fact]
    public void Episode_Navigation_IsNullable()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty(name: "Episode");
        Assert.NotNull(@object: prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(propertyInfo: prop);
        Assert.Equal(expected: NullabilityState.Nullable, actual: info.ReadState);
    }

    [Fact]
    public void NullCheck_Movie_WorksCorrectly_WhenNotLoaded()
    {
        Cast cast = new();
        bool hasMovie = cast.Movie is not null;
        Assert.False(condition: hasMovie);
    }

    [Fact]
    public void NullCheck_Tv_WorksCorrectly_WhenNotLoaded()
    {
        Cast cast = new();
        bool hasTv = cast.Tv is not null;
        Assert.False(condition: hasTv);
    }

    [Fact]
    public void NullCheck_Season_WorksCorrectly_WhenNotLoaded()
    {
        Cast cast = new();
        bool hasSeason = cast.Season is not null;
        Assert.False(condition: hasSeason);
    }

    [Fact]
    public void NullCheck_Episode_WorksCorrectly_WhenNotLoaded()
    {
        Cast cast = new();
        bool hasEpisode = cast.Episode is not null;
        Assert.False(condition: hasEpisode);
    }

    [Theory]
    [InlineData(data: "Movie")]
    [InlineData(data: "Tv")]
    [InlineData(data: "Season")]
    [InlineData(data: "Episode")]
    public void NullableNavigation_HasNoFieldInitializer_ToNew(string propertyName)
    {
        Cast cast = new();
        PropertyInfo? prop = typeof(Cast).GetProperty(name: propertyName);
        Assert.NotNull(@object: prop);
        object? value = prop.GetValue(obj: cast);
        Assert.Null(@object: value);
    }

    [Fact]
    public void Person_Navigation_IsNotNull_WithInitializer()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty(name: "Person");
        Assert.NotNull(@object: prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(propertyInfo: prop);
        Assert.Equal(expected: NullabilityState.NotNull, actual: info.ReadState);
    }

    [Fact]
    public void Role_Navigation_IsNotNull_WithInitializer()
    {
        PropertyInfo? prop = typeof(Cast).GetProperty(name: "Role");
        Assert.NotNull(@object: prop);
        NullabilityInfoContext context = new();
        NullabilityInfo info = context.Create(propertyInfo: prop);
        Assert.Equal(expected: NullabilityState.NotNull, actual: info.ReadState);
    }

    [Fact]
    public void Movie_CanBeAssigned()
    {
        Movie movie = new() { Id = 1 };
        Cast cast = new() { Movie = movie, MovieId = 1 };
        Assert.NotNull(@object: cast.Movie);
        Assert.Equal(expected: 1, actual: cast.Movie.Id);
    }

    [Fact]
    public void Movie_CanBeSetToNull()
    {
        Movie movie = new() { Id = 1 };
        Cast cast = new() { Movie = movie, MovieId = 1 };
        cast.Movie = null;
        Assert.Null(@object: cast.Movie);
    }
}
