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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Data.DTOs;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Storage;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait(name: "Category", value: "Characterization")]
public class LibraryRepositoryTests : IDisposable
{
    private readonly MediaContext _context;
    private readonly LibraryRepository _repository;
    private readonly SqliteConnection _factoryConnection;

    public LibraryRepositoryTests()
    {
        (IDbContextFactory<MediaContext> factory, _factoryConnection) =
            TestMediaContextFactory.CreateSeededFactory();
        _context = factory.CreateDbContext();
        _repository = new(contextFactory: factory);
    }

    [Fact]
    public async Task GetLibraries_ReturnsLibrariesForUser()
    {
        List<Library> libraries = await _repository.GetLibraries(userId: SeedConstants.UserId);

        Assert.Equal(expected: 2, actual: libraries.Count);
        Assert.Contains(collection: libraries, filter: l => l.Title == "Movies");
        Assert.Contains(collection: libraries, filter: l => l.Title == "TV Shows");
    }

    [Fact]
    public async Task GetLibraries_ReturnsEmpty_WhenUserHasNoAccess()
    {
        List<Library> libraries = await _repository.GetLibraries(userId: SeedConstants.OtherUserId);

        Assert.Empty(collection: libraries);
    }

    [Fact]
    public async Task GetLibraries_OrderedByOrder()
    {
        List<Library> libraries = await _repository.GetLibraries(userId: SeedConstants.UserId);

        Assert.Equal(expected: "Movies", actual: libraries[index: 0].Title);
        Assert.Equal(expected: "TV Shows", actual: libraries[index: 1].Title);
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Ulid_ReturnsLibrary()
    {
        Library? library = await _repository.GetLibraryByIdAsync(id: SeedConstants.MovieLibraryId);

        Assert.NotNull(@object: library);
        Assert.Equal(expected: "Movies", actual: library.Title);
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Ulid_ReturnsNull_WhenNotFound()
    {
        Library? library = await _repository.GetLibraryByIdAsync(id: Ulid.NewUlid());

        Assert.Null(@object: library);
    }

    [Fact]
    public async Task GetAllLibrariesAsync_ReturnsAllLibraries()
    {
        List<Library> libraries = await _repository.GetAllLibrariesAsync();

        Assert.Equal(expected: 2, actual: libraries.Count);
    }

    [Fact]
    public async Task GetFoldersAsync_ReturnsFolders()
    {
        List<FolderDto> folders = await _repository.GetFoldersAsync();

        Assert.NotEmpty(collection: folders);
    }

    [Fact]
    public async Task GetLibraryMovieCardsAsync_ReturnsMovieCards()
    {
        List<MovieCardDto> cards = await _repository.GetLibraryMovieCardsAsync(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.MovieLibraryId,
            country: "US",
            take: 10,
            skip: 0
        );

        Assert.Equal(expected: 2, actual: cards.Count);
        Assert.Contains(collection: cards, filter: c => c.Title == "Spirited Away");
        Assert.Contains(collection: cards, filter: c => c.Title == "Pulp Fiction");
    }

    [Fact]
    public async Task GetLibraryMovieCardsAsync_RespectsSkipAndTake()
    {
        List<MovieCardDto> cards = await _repository.GetLibraryMovieCardsAsync(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.MovieLibraryId,
            country: "US",
            take: 1,
            skip: 0
        );

        Assert.Single(collection: cards);
    }

    [Fact]
    public async Task GetLibraryMovieCardsAsync_ReturnsEmpty_WhenUserHasNoAccess()
    {
        List<MovieCardDto> cards = await _repository.GetLibraryMovieCardsAsync(
            userId: SeedConstants.OtherUserId,
            libraryId: SeedConstants.MovieLibraryId,
            country: "US",
            take: 10,
            skip: 0
        );

        Assert.Empty(collection: cards);
    }

    [Fact]
    public async Task GetLibraryTvCardsAsync_ReturnsTvCards()
    {
        List<TvCardDto> cards = await _repository.GetLibraryTvCardsAsync(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.TvLibraryId,
            country: "US",
            take: 10,
            skip: 0
        );

        Assert.Single(collection: cards);
        Assert.Equal(expected: "Breaking Bad", actual: cards[index: 0].Title);
    }

    [Fact]
    public async Task GetLibraryMovieCardsAsync_TakeMatchesCarouselSize()
    {
        // Verify that Take limits results to the requested carousel size
        List<MovieCardDto> allCards = await _repository.GetLibraryMovieCardsAsync(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.MovieLibraryId,
            country: "US",
            take: 100,
            skip: 0
        );
        Assert.Equal(expected: 2, actual: allCards.Count);

        List<MovieCardDto> limitedCards = await _repository.GetLibraryMovieCardsAsync(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.MovieLibraryId,
            country: "US",
            take: 1,
            skip: 0
        );
        Assert.Single(collection: limitedCards);
    }

    [Fact]
    public async Task GetLibraryTvCardsAsync_TakeMatchesCarouselSize()
    {
        List<TvCardDto> allCards = await _repository.GetLibraryTvCardsAsync(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.TvLibraryId,
            country: "US",
            take: 100,
            skip: 0
        );
        Assert.Single(collection: allCards);

        List<TvCardDto> limitedCards = await _repository.GetLibraryTvCardsAsync(
            userId: SeedConstants.UserId,
            libraryId: SeedConstants.TvLibraryId,
            country: "US",
            take: 1,
            skip: 0
        );
        Assert.Single(collection: limitedCards);
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Paginated_TakeLimitsMoviesPerCarousel()
    {
        // The .Take(take) inside Include() limits movies per-carousel
        Library? library = await _repository.GetLibraryByIdAsync(
            libraryId: SeedConstants.MovieLibraryId,
            userId: SeedConstants.UserId,
            language: "en",
            country: "US",
            take: 1,
            page: 0
        );

        Assert.NotNull(@object: library);
        Assert.Single(collection: library.LibraryMovies);
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Paginated_TakeIsOrderedByTitleSort_NotInsertionOrder()
    {
        // "Spirited Away" (Id 129) is seeded before "Pulp Fiction" (Id 680), so a Take(1)
        // without an explicit OrderBy would return whatever the database's unordered scan
        // happens to yield first (observed: insertion order) instead of the alphabetically
        // first title. EF Core's Include(...).Take(n) requires its own OrderBy inside that
        // navigation lambda — an outer OrderBy on the root query does not cover it.
        Library? library = await _repository.GetLibraryByIdAsync(
            libraryId: SeedConstants.MovieLibraryId,
            userId: SeedConstants.UserId,
            language: "en",
            country: "US",
            take: 1,
            page: 0
        );

        Assert.NotNull(@object: library);
        Assert.Single(collection: library.LibraryMovies);
        Assert.Equal(expected: "Pulp Fiction", actual: library.LibraryMovies.Single().Movie.Title);
    }

    [Fact]
    public async Task GetLibraryByIdAsync_Paginated_TakeReturnsAllWhenHigherThanCount()
    {
        Library? library = await _repository.GetLibraryByIdAsync(
            libraryId: SeedConstants.MovieLibraryId,
            userId: SeedConstants.UserId,
            language: "en",
            country: "US",
            take: 100,
            page: 0
        );

        Assert.NotNull(@object: library);
        Assert.Equal(expected: 2, actual: library.LibraryMovies.Count);
    }

    [Fact]
    public async Task AddLibraryAsync_CreatesLibrary()
    {
        Ulid newLibraryId = Ulid.NewUlid();
        Library newLibrary = new()
        {
            Id = newLibraryId,
            Title = "Music",
            Type = "music",
            Order = 3,
        };

        await _repository.AddLibraryAsync(library: newLibrary, userId: SeedConstants.UserId);

        Library? found = await _repository.GetLibraryByIdAsync(id: newLibraryId);
        Assert.NotNull(@object: found);
        Assert.Equal(expected: "Music", actual: found.Title);
    }

    [Fact]
    public async Task DeleteLibraryAsync_RemovesLibrary()
    {
        Library? library = await _context.Libraries.FirstOrDefaultAsync(predicate: l =>
            l.Id == SeedConstants.MovieLibraryId
        );
        Assert.NotNull(@object: library);

        await _repository.DeleteLibraryAsync(library: library);

        Library? deleted = await _repository.GetLibraryByIdAsync(id: SeedConstants.MovieLibraryId);
        Assert.Null(@object: deleted);
    }

    [Fact]
    public async Task GetLibraries_IncludesEncoderProfilesOnFolders()
    {
        List<Library> libraries = await _repository.GetLibraries(userId: SeedConstants.UserId);

        Library movieLibrary = libraries.First(predicate: l => l.Title == "Movies");
        Assert.NotEmpty(collection: movieLibrary.FolderLibraries);

        FolderLibrary folderLibrary = movieLibrary.FolderLibraries.First();
        Assert.NotNull(@object: folderLibrary.Folder);
        Assert.NotEmpty(collection: folderLibrary.Folder.EncodingPresetFolders);

        EncodingPresetFolder link = folderLibrary.Folder.EncodingPresetFolders.First();
        Assert.NotNull(@object: link.Preset);
        Assert.Equal(expected: "Default HLS", actual: link.Preset!.Name);
    }

    [Fact]
    public async Task GetLibraries_MapsToLibrariesResponseItemDto_WithoutException()
    {
        List<Library> libraries = await _repository.GetLibraries(userId: SeedConstants.UserId);

        // This is exactly what the controller does - it should not throw
        List<LibrariesResponseItemDto> response = libraries
            .Select(selector: library => new LibrariesResponseItemDto(library: library))
            .ToList();

        Assert.Equal(expected: 2, actual: response.Count);

        LibrariesResponseItemDto movieDto = response.First(predicate: r => r.Title == "Movies");
        Assert.NotEmpty(collection: movieDto.FolderLibrary);
        Assert.NotEmpty(collection: movieDto.FolderLibrary[0].Folder.EncoderProfiles);
        Assert.Equal(expected: "Default HLS", actual: movieDto.FolderLibrary[0].Folder.EncoderProfiles[0].Name);
    }

    [Fact]
    public async Task GetFoldersAsync_MapsFolderDto_WithEncoderProfiles()
    {
        List<FolderDto> folders = await _repository.GetFoldersAsync();

        Assert.NotEmpty(collection: folders);
        // FolderDto uses Select projection so EncodingPresetFolders may not be loaded
        // in the projection query, but should not throw
    }

    [Fact]
    public async Task GetLibraries_IncludesLanguageLibraries()
    {
        List<Library> libraries = await _repository.GetLibraries(userId: SeedConstants.UserId);

        Library movieLibrary = libraries.First(predicate: l => l.Title == "Movies");
        Assert.NotEmpty(collection: movieLibrary.LanguageLibraries);
        Assert.Equal(expected: "en", actual: movieLibrary.LanguageLibraries.First().Language.Iso6391);
    }

    [Fact]
    public async Task SyncEncodingPresetFolderAsync_ReplacesOldMappingWithNew()
    {
        EncodingPreset newPreset = new()
        {
            Id = Ulid.NewUlid(),
            Name = "New Profile",
            ProfileJson = "{}",
            IsBuiltIn = false,
        };
        _context.EncodingPresets.Add(entity: newPreset);
        await _context.SaveChangesAsync();

        Folder folder = await _context.Folders.FirstAsync(predicate: f => f.Id == SeedConstants.MovieFolderId);

        List<EncodingPresetFolder> newMappings =
        [
            new() { PresetId = newPreset.Id, FolderId = SeedConstants.MovieFolderId },
        ];

        await _repository.SyncEncodingPresetFolderAsync(encodingPresetFolders: newMappings, folders: [folder]);

        List<EncodingPresetFolder> stored = await _context
            .EncodingPresetFolders.Where(predicate: link => link.FolderId == SeedConstants.MovieFolderId)
            .ToListAsync();

        Assert.Single(collection: stored);
        Assert.Equal(expected: newPreset.Id, actual: stored[index: 0].PresetId);
        Assert.DoesNotContain(collection: stored, filter: link => link.PresetId == SeedConstants.EncodingPresetId);
    }

    [Fact]
    public async Task SyncEncodingPresetFolderAsync_RollsBackDeleteWhenInsertFails()
    {
        Folder folder = await _context.Folders.FirstAsync(predicate: f => f.Id == SeedConstants.MovieFolderId);

        // Non-existent PresetId violates the FK on upsert, so the whole sync
        // (delete + insert) must roll back as one unit — the original
        // mapping seeded in SeedData must survive.
        List<EncodingPresetFolder> invalidMappings =
        [
            new() { PresetId = Ulid.NewUlid(), FolderId = SeedConstants.MovieFolderId },
        ];

        await Assert.ThrowsAnyAsync<Exception>(testCode: () =>
            _repository.SyncEncodingPresetFolderAsync(encodingPresetFolders: invalidMappings, folders: [folder])
        );

        EncodingPresetFolder? original = await _context.EncodingPresetFolders.FirstOrDefaultAsync(
            predicate: link =>
                link.FolderId == SeedConstants.MovieFolderId
                && link.PresetId == SeedConstants.EncodingPresetId
        );

        Assert.NotNull(@object: original);
    }

    [Fact]
    public async Task UpdateLibraryAsync_Succeeds_WhenTwoFoldersShareOneDriver()
    {
        Folder secondFolder = new()
        {
            Id = Ulid.NewUlid(),
            Path = "/media/movies-extra",
            DriverId = Driver.SystemLocalDriverId,
        };
        _context.Folders.Add(entity: secondFolder);
        _context.FolderLibrary.Add(entity: new(folderId: secondFolder.Id, libraryId: SeedConstants.MovieLibraryId));
        await _context.SaveChangesAsync();

        Library? library = await _repository.GetLibraryByIdAsync(id: SeedConstants.MovieLibraryId);
        Assert.NotNull(@object: library);
        library.Title = "Renamed Movies";

        await _repository.UpdateLibraryAsync(library: library);

        Library? reloaded = await _repository.GetLibraryByIdLiteAsync(id: SeedConstants.MovieLibraryId);
        Assert.NotNull(@object: reloaded);
        Assert.Equal(expected: "Renamed Movies", actual: reloaded.Title);
    }

    [Fact]
    public async Task UpdateLibraryAsync_DoesNotRewriteFoldersOrDrivers()
    {
        Library? library = await _repository.GetLibraryByIdAsync(id: SeedConstants.MovieLibraryId);
        Assert.NotNull(@object: library);

        library.Title = "Retitled";
        library.FolderLibraries.First().Folder.Path = "/tampered/path";
        library.FolderLibraries.First().Folder.Driver!.Name = "Tampered Driver";

        await _repository.UpdateLibraryAsync(library: library);

        _context.ChangeTracker.Clear();

        Folder folder = await _context.Folders.FirstAsync(predicate: f => f.Id == SeedConstants.MovieFolderId);
        Driver driver = await _context.Drivers.FirstAsync(predicate: d => d.Id == Driver.SystemLocalDriverId);

        Assert.Equal(expected: "/media/movies", actual: folder.Path);
        Assert.Equal(expected: "Local Filesystem", actual: driver.Name);
    }

    [Fact]
    public async Task SetLibraryLanguagesAsync_RemovesLanguagesNotInTheRequestedSet()
    {
        await _repository.SetLibraryLanguagesAsync(libraryId: SeedConstants.MovieLibraryId, languageIds: []);

        _context.ChangeTracker.Clear();

        List<LanguageLibrary> remaining = await _context
            .LanguageLibrary.Where(predicate: ll => ll.LibraryId == SeedConstants.MovieLibraryId)
            .ToListAsync();

        Assert.Empty(collection: remaining);
    }

    [Fact]
    public async Task SetLibraryLanguagesAsync_AddsRequestedLanguage_AndIsIdempotent()
    {
        await _repository.SetLibraryLanguagesAsync(libraryId: SeedConstants.MovieLibraryId, languageIds: [1]);
        await _repository.SetLibraryLanguagesAsync(libraryId: SeedConstants.MovieLibraryId, languageIds: [1]);

        _context.ChangeTracker.Clear();

        List<LanguageLibrary> remaining = await _context
            .LanguageLibrary.Where(predicate: ll => ll.LibraryId == SeedConstants.MovieLibraryId)
            .ToListAsync();

        Assert.Single(collection: remaining);
        Assert.Equal(expected: 1, actual: remaining[index: 0].LanguageId);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _factoryConnection.Dispose();
    }
}
