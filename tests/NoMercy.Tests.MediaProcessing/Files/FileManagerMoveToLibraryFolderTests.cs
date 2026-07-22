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
using Moq;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Storage;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Analysis;
using NoMercy.MediaProcessing.Files;
using NoMercy.NmSystem.Domain;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.MediaProcessing.Files;

// ---------------------------------------------------------------------------
// FileManager.MoveToLibraryFolder moves a movie/show's on-disk folder to a
// different library's storage backend and repoints every DB row (Movie/Tv,
// LibraryMovie/LibraryTv) at the new library — the "move to library" admin
// action. Seeds real Library/Driver/Folder/LibraryMovie/LibraryTv rows into
// the shared file-backed MediaContext (same DB the method opens internally)
// and moves real files on a real LocalStorage, exactly like a live move.
// ---------------------------------------------------------------------------
[Trait(name: "Category", value: "Unit")]
public sealed class FileManagerMoveToLibraryFolderTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly List<Ulid> _libraryIds = [];
    private readonly List<Ulid> _folderIds = [];
    private readonly List<Ulid> _driverIds = [];
    private readonly List<int> _movieIds = [];
    private readonly List<int> _tvIds = [];

    public FileManagerMoveToLibraryFolderTests()
    {
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-movelib-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempRoot))
            Directory.Delete(path: _tempRoot, recursive: true);

        using MediaContext cleanup = new();
        cleanup.LibraryMovie.RemoveRange(
            entities: cleanup.LibraryMovie.Where(predicate: lm => _movieIds.Contains(lm.MovieId))
        );
        cleanup.LibraryTv.RemoveRange(entities: cleanup.LibraryTv.Where(predicate: lt => _tvIds.Contains(lt.TvId)));
        cleanup.Movies.RemoveRange(entities: cleanup.Movies.Where(predicate: m => _movieIds.Contains(m.Id)));
        cleanup.Tvs.RemoveRange(entities: cleanup.Tvs.Where(predicate: t => _tvIds.Contains(t.Id)));
        cleanup.SaveChanges();
        cleanup.FolderLibrary.RemoveRange(
            entities: cleanup.FolderLibrary.Where(predicate: fl => _libraryIds.Contains(fl.LibraryId))
        );
        cleanup.Folders.RemoveRange(entities: cleanup.Folders.Where(predicate: f => _folderIds.Contains(f.Id)));
        cleanup.Libraries.RemoveRange(entities: cleanup.Libraries.Where(predicate: l => _libraryIds.Contains(l.Id)));
        cleanup.SaveChanges();
        cleanup.Drivers.RemoveRange(entities: cleanup.Drivers.Where(predicate: d => _driverIds.Contains(d.Id)));
        cleanup.SaveChanges();
    }

    private (Library Library, Folder Folder) SeedLibraryWithFolder(
        string libraryType,
        string folderPath
    )
    {
        Ulid libraryId = Ulid.NewUlid();
        _libraryIds.Add(item: libraryId);

        Driver driver = new()
        {
            Id = Ulid.NewUlid(),
            Name = $"test-driver-{Ulid.NewUlid()}",
            Type = "local",
        };
        _driverIds.Add(item: driver.Id);

        Folder folder = new()
        {
            Id = Ulid.NewUlid(),
            Path = folderPath,
            DriverId = driver.Id,
        };
        _folderIds.Add(item: folder.Id);

        Library library = new()
        {
            Id = libraryId,
            Title = "Test Library",
            Type = libraryType,
        };

        using MediaContext context = new();
        context.Drivers.Add(entity: driver);
        context.Libraries.Add(entity: library);
        context.Folders.Add(entity: folder);
        context.FolderLibrary.Add(entity: new(folderId: folder.Id, libraryId: libraryId));
        context.SaveChanges();

        return (library, folder);
    }

    private static FileManager BuildManager(Mock<IFileRepository> repoMock, IStorageFactory factory)
    {
        Mock<IStorageDriver> driverMock = new();
        Mock<IMediaAnalyzer> mediaAnalyzerMock = new();
        return new(fileRepository: repoMock.Object, storageFactory: factory, storageDriver: driverMock.Object, mediaAnalyzer: mediaAnalyzerMock.Object);
    }

    [Fact]
    public async Task MoveToLibraryFolder_MovieFolderMissingEverywhere_LogsAndReturnsWithoutThrowing()
    {
        (Library sourceLibrary, Folder sourceFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: Path.Combine(path1: _tempRoot, path2: "source-empty")
        );
        Directory.CreateDirectory(path: sourceFolder.Path);

        int movieId = 500_001;
        _movieIds.Add(item: movieId);
        Movie movie = new()
        {
            Id = movieId,
            Title = "Ghost Movie",
            Folder = "Never.On.Disk.2020",
            LibraryId = sourceLibrary.Id,
        };
        using (MediaContext seed = new())
        {
            seed.Movies.Add(entity: movie);
            seed.SaveChanges();
        }

        Mock<IStorageFactory> factoryMock = new();
        LocalStorageDriver driver = new();
        factoryMock
            .Setup(expression: f => f.For(sourceFolder.Id, sourceFolder.DriverId, string.Empty))
            .Returns(value: new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver)));

        Mock<IFileRepository> repoMock = new();
        FileManager manager = BuildManager(repoMock: repoMock, factory: factoryMock.Object);

        (Library destLibrary, Folder destFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: Path.Combine(path1: _tempRoot, path2: "dest-unused")
        );

        Func<Task> act = () => manager.MoveToLibraryFolder(id: movieId, folder: destFolder);

        await act.Should()
            .NotThrowAsync(because: "a folder that resolves nowhere must log and return, never throw");

        using MediaContext verify = new();
        Movie? unchanged = await verify.Movies.FirstOrDefaultAsync(predicate: m => m.Id == movieId);
        unchanged!
            .LibraryId.Should()
            .Be(expected: sourceLibrary.Id, because: "no folder found means no library move happens");
    }

    [Fact]
    public async Task MoveToLibraryFolder_MovieFound_MovesFilesAndRepointsLibraryAndLibraryMovie()
    {
        (Library sourceLibrary, Folder sourceFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: Path.Combine(path1: _tempRoot, path2: "source-lib")
        );
        (Library destLibrary, Folder destFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: Path.Combine(path1: _tempRoot, path2: "dest-lib")
        );
        // Directory.Move (the same-backend fast path) requires the
        // destination's PARENT to already exist — the configured library
        // root, in production terms.
        Directory.CreateDirectory(path: destFolder.Path);

        string movieFolderName = "My.Movie.2020";
        string sourceMovieDir = Path.Combine(path1: sourceFolder.Path, path2: movieFolderName);
        Directory.CreateDirectory(path: sourceMovieDir);
        // A non-media file: keeps the move meaningfully non-empty without
        // MediaScan's post-move rescan invoking the real ffprobe binary
        // (info.nfo doesn't match any of MediaScan's extension filters).
        File.WriteAllText(path: Path.Combine(path1: sourceMovieDir, path2: "info.nfo"), contents: "movie info");

        int movieId = 500_002;
        _movieIds.Add(item: movieId);
        Movie movie = new()
        {
            Id = movieId,
            Title = "Real Movie",
            Folder = movieFolderName,
            LibraryId = sourceLibrary.Id,
        };
        using (MediaContext seed = new())
        {
            seed.Movies.Add(entity: movie);
            seed.LibraryMovie.Add(entity: new(libraryId: sourceLibrary.Id, movieId: movieId));
            seed.SaveChanges();
        }

        LocalStorageDriver sourceDriver = new();
        LocalStorageDriver destDriver = new();
        IStorage sourceStorage = new LocalStorage(
            driver: sourceDriver,
            guard: new StoragePathGuard(allowedRoots: [], driver: sourceDriver)
        );
        IStorage destStorage = new LocalStorage(driver: destDriver, guard: new StoragePathGuard(allowedRoots: [], driver: destDriver));

        Mock<IStorageFactory> factoryMock = new();
        factoryMock
            .Setup(expression: f => f.For(sourceFolder.Id, sourceFolder.DriverId, string.Empty))
            .Returns(value: sourceStorage);
        factoryMock
            .Setup(expression: f => f.For(destFolder.Id, destFolder.DriverId, string.Empty))
            .Returns(value: destStorage);

        Mock<IFileRepository> repoMock = new();
        repoMock
            .Setup(expression: r => r.MediaType(movieId, It.IsAny<Library>()))
            .ReturnsAsync(value: (movie, (Tv?)null, MediaTypes.MovieMediaType));

        FileManager manager = BuildManager(repoMock: repoMock, factory: factoryMock.Object);

        await manager.MoveToLibraryFolder(id: movieId, folder: destFolder);

        Directory
            .Exists(path: sourceMovieDir)
            .Should()
            .BeFalse(because: "the source folder must be gone after a same-backend move");
        string destMovieDir = Path.Combine(path1: destFolder.Path, path2: movieFolderName);
        Directory
            .Exists(path: destMovieDir)
            .Should()
            .BeTrue(because: "the folder must exist under the new library root");
        File.Exists(path: Path.Combine(path1: destMovieDir, path2: "info.nfo")).Should().BeTrue();

        using MediaContext verify = new();
        Movie? moved = await verify.Movies.FirstOrDefaultAsync(predicate: m => m.Id == movieId);
        moved!
            .LibraryId.Should()
            .Be(expected: destLibrary.Id, because: "the movie must be repointed at the destination library");
        moved.Folder.Should().Be(expected: movieFolderName);

        LibraryMovie? libraryMovie = await verify.LibraryMovie.FirstOrDefaultAsync(predicate: lm =>
            lm.MovieId == movieId
        );
        libraryMovie.Should().NotBeNull();
        libraryMovie!
            .LibraryId.Should()
            .Be(expected: destLibrary.Id, because: "the LibraryMovie link must follow the move too");
    }

    [Fact]
    public async Task MoveToLibraryFolder_TvFound_MovesFilesAndRepointsLibraryAndLibraryTv()
    {
        (Library sourceLibrary, Folder sourceFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.TvMediaType,
            folderPath: Path.Combine(path1: _tempRoot, path2: "source-tv-lib")
        );
        (Library destLibrary, Folder destFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.TvMediaType,
            folderPath: Path.Combine(path1: _tempRoot, path2: "dest-tv-lib")
        );
        Directory.CreateDirectory(path: destFolder.Path);

        string showFolderName = "My.Tv.Show";
        string sourceShowDir = Path.Combine(path1: sourceFolder.Path, path2: showFolderName);
        Directory.CreateDirectory(path: sourceShowDir);
        File.WriteAllText(path: Path.Combine(path1: sourceShowDir, path2: "info.nfo"), contents: "show info");

        int tvId = 500_003;
        _tvIds.Add(item: tvId);
        Tv show = new()
        {
            Id = tvId,
            Title = "Real Show",
            Folder = showFolderName,
            LibraryId = sourceLibrary.Id,
        };
        using (MediaContext seed = new())
        {
            seed.Tvs.Add(entity: show);
            seed.LibraryTv.Add(entity: new(libraryId: sourceLibrary.Id, tvId: tvId));
            seed.SaveChanges();
        }

        LocalStorageDriver sourceDriver = new();
        LocalStorageDriver destDriver = new();
        IStorage sourceStorage = new LocalStorage(
            driver: sourceDriver,
            guard: new StoragePathGuard(allowedRoots: [], driver: sourceDriver)
        );
        IStorage destStorage = new LocalStorage(driver: destDriver, guard: new StoragePathGuard(allowedRoots: [], driver: destDriver));

        Mock<IStorageFactory> factoryMock = new();
        factoryMock
            .Setup(expression: f => f.For(sourceFolder.Id, sourceFolder.DriverId, string.Empty))
            .Returns(value: sourceStorage);
        factoryMock
            .Setup(expression: f => f.For(destFolder.Id, destFolder.DriverId, string.Empty))
            .Returns(value: destStorage);

        Mock<IFileRepository> repoMock = new();
        repoMock
            .Setup(expression: r => r.MediaType(tvId, It.IsAny<Library>()))
            .ReturnsAsync(value: ((Movie?)null, show, MediaTypes.TvMediaType));

        FileManager manager = BuildManager(repoMock: repoMock, factory: factoryMock.Object);

        await manager.MoveToLibraryFolder(id: tvId, folder: destFolder);

        Directory.Exists(path: sourceShowDir).Should().BeFalse();
        string destShowDir = Path.Combine(path1: destFolder.Path, path2: showFolderName);
        Directory.Exists(path: destShowDir).Should().BeTrue();

        using MediaContext verify = new();
        Tv? moved = await verify.Tvs.FirstOrDefaultAsync(predicate: t => t.Id == tvId);
        moved!.LibraryId.Should().Be(expected: destLibrary.Id);

        LibraryTv? libraryTv = await verify.LibraryTv.FirstOrDefaultAsync(predicate: lt => lt.TvId == tvId);
        libraryTv.Should().NotBeNull();
        libraryTv!.LibraryId.Should().Be(expected: destLibrary.Id);
    }
}
