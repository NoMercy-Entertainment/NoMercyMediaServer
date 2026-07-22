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
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Storage;
using NoMercy.Database.Models.TvShows;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

public class ForUserExtensionTests
{
    private readonly Guid _userId = SeedConstants.UserId;
    private readonly Guid _otherUserId = SeedConstants.OtherUserId;

    [Fact]
    public async Task ForUser_Movie_ReturnsOnlyAccessibleMovies()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Movie> movies = await context.Movies.AsNoTracking().ForUser(userId: _userId).ToListAsync();

        Assert.Equal(expected: 2, actual: movies.Count);
        Assert.Contains(collection: movies, filter: m => m.Title == "Spirited Away");
        Assert.Contains(collection: movies, filter: m => m.Title == "Pulp Fiction");
    }

    [Fact]
    public async Task ForUser_Movie_ExcludesUnauthorizedUser()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Movie> movies = await context
            .Movies.AsNoTracking()
            .ForUser(userId: _otherUserId)
            .ToListAsync();

        Assert.Empty(collection: movies);
    }

    [Fact]
    public async Task ForUser_Tv_ReturnsOnlyAccessibleShows()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Tv> shows = await context.Tvs.AsNoTracking().ForUser(userId: _userId).ToListAsync();

        Assert.Single(collection: shows);
        Assert.Equal(expected: "Breaking Bad", actual: shows[index: 0].Title);
    }

    [Fact]
    public async Task ForUser_Tv_ExcludesUnauthorizedUser()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Tv> shows = await context.Tvs.AsNoTracking().ForUser(userId: _otherUserId).ToListAsync();

        Assert.Empty(collection: shows);
    }

    [Fact]
    public async Task ForUser_Library_ReturnsOnlyAccessibleLibraries()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Library> libraries = await context
            .Libraries.AsNoTracking()
            .ForUser(userId: _userId)
            .ToListAsync();

        Assert.Equal(expected: 2, actual: libraries.Count);
        Assert.Contains(collection: libraries, filter: l => l.Title == "Movies");
        Assert.Contains(collection: libraries, filter: l => l.Title == "TV Shows");
    }

    [Fact]
    public async Task ForUser_Library_ExcludesUnauthorizedUser()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        List<Library> libraries = await context
            .Libraries.AsNoTracking()
            .ForUser(userId: _otherUserId)
            .ToListAsync();

        Assert.Empty(collection: libraries);
    }

    [Fact]
    public async Task ForUser_Collection_ReturnsOnlyAccessibleCollections()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        // Add a collection in the movie library
        Collection collection = new()
        {
            Id = 1001,
            Title = "Test Collection",
            TitleSort = "test collection",
            LibraryId = SeedConstants.MovieLibraryId,
        };
        context.Collections.Add(entity: collection);
        await context.SaveChangesAsync();

        List<Collection> collections = await context
            .Collections.AsNoTracking()
            .ForUser(userId: _userId)
            .ToListAsync();

        Assert.Single(collection: collections);
        Assert.Equal(expected: "Test Collection", actual: collections[index: 0].Title);

        List<Collection> otherUserCollections = await context
            .Collections.AsNoTracking()
            .ForUser(userId: _otherUserId)
            .ToListAsync();

        Assert.Empty(collection: otherUserCollections);
    }

    [Fact]
    public async Task ForUser_Album_ReturnsOnlyAccessibleAlbums()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        // Add an album to the existing movie library (has user access already seeded)
        Folder folder = await context.Folders.FirstAsync();
        context.Albums.Add(
            entity: new()
            {
                Id = Guid.NewGuid(),
                Name = "Test Album",
                LibraryId = SeedConstants.MovieLibraryId,
                FolderId = folder.Id,
                Library = null!,
                LibraryFolder = null!,
            }
        );
        await context.SaveChangesAsync();

        List<Album> albums = await context.Albums.AsNoTracking().ForUser(userId: _userId).ToListAsync();

        Assert.Single(collection: albums);
        Assert.Equal(expected: "Test Album", actual: albums[index: 0].Name);

        List<Album> otherUserAlbums = await context
            .Albums.AsNoTracking()
            .ForUser(userId: _otherUserId)
            .ToListAsync();

        Assert.Empty(collection: otherUserAlbums);
    }

    [Fact]
    public async Task ForUser_Artist_ReturnsOnlyAccessibleArtists()
    {
        MediaContext context = TestMediaContextFactory.CreateContext();

        // Set up music library with user access
        Ulid musicLibraryId = Ulid.NewUlid();
        Ulid musicFolderId = Ulid.NewUlid();
        context.Libraries.Add(
            entity: new()
            {
                Id = musicLibraryId,
                Title = "Music",
                Type = "music",
                Order = 3,
            }
        );
        context.Drivers.Add(
            entity: new()
            {
                Id = Driver.SystemLocalDriverId,
                Name = "Local Filesystem",
                Type = "local",
                Config = """{"rootPath":"/"}""",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );
        context.Folders.Add(
            entity: new()
            {
                Id = musicFolderId,
                Path = "/media/music",
                DriverId = Driver.SystemLocalDriverId,
            }
        );
        context.Users.Add(
            entity: new()
            {
                Id = _userId,
                Email = "test@nomercy.tv",
                Name = "Test User",
                Owner = true,
                Allowed = true,
            }
        );
        context.LibraryUser.Add(entity: new(libraryId: musicLibraryId, userId: _userId));
        context.Artists.Add(
            entity: new()
            {
                Id = Guid.NewGuid(),
                Name = "Test Artist",
                HostFolder = "/media/music/TestArtist",
                LibraryId = musicLibraryId,
                FolderId = musicFolderId,
            }
        );
        await context.SaveChangesAsync();

        List<Artist> artists = await context.Artists.AsNoTracking().ForUser(userId: _userId).ToListAsync();

        Assert.Single(collection: artists);
        Assert.Equal(expected: "Test Artist", actual: artists[index: 0].Name);

        List<Artist> otherUserArtists = await context
            .Artists.AsNoTracking()
            .ForUser(userId: _otherUserId)
            .ToListAsync();

        Assert.Empty(collection: otherUserArtists);
    }

    [Fact]
    public async Task ForUser_ChainsWithOtherLinqOperators()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        Movie? movie = await context
            .Movies.AsNoTracking()
            .Where(predicate: m => m.Id == 129)
            .ForUser(userId: _userId)
            .FirstOrDefaultAsync();

        Assert.NotNull(@object: movie);
        Assert.Equal(expected: "Spirited Away", actual: movie.Title);
    }

    [Fact]
    public async Task ForUser_WorksWithCountAndAggregates()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        int movieCount = await context.Movies.AsNoTracking().ForUser(userId: _userId).CountAsync();

        Assert.Equal(expected: 2, actual: movieCount);

        int otherUserCount = await context.Movies.AsNoTracking().ForUser(userId: _otherUserId).CountAsync();

        Assert.Equal(expected: 0, actual: otherUserCount);
    }

    [Fact]
    public async Task ForUser_MultipleLibraryAccess_ReturnsFromAllLibraries()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        // The seeded user has access to both movie and TV libraries
        // ForUser on Movies should return movies, ForUser on Tvs should return shows
        int movieCount = await context.Movies.AsNoTracking().ForUser(userId: _userId).CountAsync();
        int tvCount = await context.Tvs.AsNoTracking().ForUser(userId: _userId).CountAsync();

        Assert.Equal(expected: 2, actual: movieCount);
        Assert.Equal(expected: 1, actual: tvCount);
    }

    [Fact]
    public async Task ForUser_PartialLibraryAccess_OnlyReturnsAccessibleContent()
    {
        MediaContext context = TestMediaContextFactory.CreateSeededContext();

        // Add a second user with access to only the TV library
        Guid partialUserId = Guid.NewGuid();
        context.Users.Add(
            entity: new()
            {
                Id = partialUserId,
                Email = "partial@nomercy.tv",
                Name = "Partial User",
                Owner = false,
                Allowed = true,
            }
        );
        context.LibraryUser.Add(entity: new(libraryId: SeedConstants.TvLibraryId, userId: partialUserId));
        await context.SaveChangesAsync();

        // Partial user should see TV shows but not movies
        List<Movie> movies = await context
            .Movies.AsNoTracking()
            .ForUser(userId: partialUserId)
            .ToListAsync();
        List<Tv> shows = await context.Tvs.AsNoTracking().ForUser(userId: partialUserId).ToListAsync();

        Assert.Empty(collection: movies);
        Assert.Single(collection: shows);
        Assert.Equal(expected: "Breaking Bad", actual: shows[index: 0].Title);
    }

    [Fact]
    public async Task ForUser_GeneratesExistsClauseInSql()
    {
        (MediaContext context, SqlCaptureInterceptor interceptor) =
            TestMediaContextFactory.CreateSeededContextWithInterceptor();

        await context.Movies.AsNoTracking().ForUser(userId: _userId).ToListAsync();

        string sql = string.Join(separator: " ", values: interceptor.CapturedSql);
        Assert.Contains(expectedSubstring: "EXISTS", actualString: sql, comparisonType: StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedSubstring: "LibraryUser", actualString: sql, comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}
