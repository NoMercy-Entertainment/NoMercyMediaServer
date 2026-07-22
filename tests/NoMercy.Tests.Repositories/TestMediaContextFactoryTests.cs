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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class TestMediaContextFactoryTests : IDisposable
{
    private readonly MediaContext _context;

    public TestMediaContextFactoryTests()
    {
        _context = TestMediaContextFactory.CreateSeededContext();
    }

    [Fact]
    public void CreateContext_CreatesEmptyDatabase()
    {
        using MediaContext emptyContext = TestMediaContextFactory.CreateContext();
        Assert.Empty(collection: emptyContext.Users);
    }

    [Fact]
    public void CreateSeededContext_SeedsUser()
    {
        User? user = _context.Users.FirstOrDefault(predicate: u => u.Id == SeedConstants.UserId);
        Assert.NotNull(@object: user);
        Assert.Equal(expected: "Test User", actual: user.Name);
        Assert.True(condition: user.Owner);
    }

    [Fact]
    public void CreateSeededContext_SeedsLibraries()
    {
        List<Library> libraries = _context.Libraries.ToList();
        Assert.Equal(expected: 2, actual: libraries.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsLibraryUserAccess()
    {
        List<LibraryUser> libraryUsers = _context
            .LibraryUser.Where(predicate: lu => lu.UserId == SeedConstants.UserId)
            .ToList();
        Assert.Equal(expected: 2, actual: libraryUsers.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsMovies()
    {
        List<Movie> movies = _context.Movies.ToList();
        Assert.Equal(expected: 2, actual: movies.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsTvShows()
    {
        List<Tv> shows = _context.Tvs.ToList();
        Assert.Single(collection: shows);
    }

    [Fact]
    public void CreateSeededContext_SeedsVideoFiles()
    {
        List<VideoFile> videoFiles = _context.VideoFiles.ToList();
        Assert.Equal(expected: 4, actual: videoFiles.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsEpisodes()
    {
        List<Episode> episodes = _context.Episodes.ToList();
        Assert.Equal(expected: 2, actual: episodes.Count);
    }

    [Fact]
    public void CreateSeededContext_SeedsGenres()
    {
        List<Genre> genres = _context.Genres.ToList();
        Assert.Equal(expected: 2, actual: genres.Count);
    }

    [Fact]
    public async Task CreateSeededContext_MovieLibraryJoinWorks()
    {
        List<LibraryMovie> libraryMovies = await _context.LibraryMovie.ToListAsync();
        Assert.Equal(expected: 2, actual: libraryMovies.Count);
    }

    [Fact]
    public async Task CreateSeededContext_TvLibraryJoinWorks()
    {
        List<LibraryTv> libraryTvs = await _context.LibraryTv.ToListAsync();
        Assert.Single(collection: libraryTvs);
    }

    [Fact]
    public void EachContextIsIsolated()
    {
        using MediaContext context1 = TestMediaContextFactory.CreateSeededContext();
        using MediaContext context2 = TestMediaContextFactory.CreateContext();

        Assert.NotEmpty(collection: context1.Users);
        Assert.Empty(collection: context2.Users);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
