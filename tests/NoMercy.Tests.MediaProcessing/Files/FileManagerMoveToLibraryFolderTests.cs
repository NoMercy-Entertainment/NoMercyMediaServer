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
[Trait("Category", "Unit")]
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
        _tempRoot = Path.Combine(Path.GetTempPath(), $"nm-movelib-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);

        using MediaContext cleanup = new();
        cleanup.LibraryMovie.RemoveRange(
            cleanup.LibraryMovie.Where(lm => _movieIds.Contains(lm.MovieId))
        );
        cleanup.LibraryTv.RemoveRange(cleanup.LibraryTv.Where(lt => _tvIds.Contains(lt.TvId)));
        cleanup.Movies.RemoveRange(cleanup.Movies.Where(m => _movieIds.Contains(m.Id)));
        cleanup.Tvs.RemoveRange(cleanup.Tvs.Where(t => _tvIds.Contains(t.Id)));
        cleanup.SaveChanges();
        cleanup.FolderLibrary.RemoveRange(
            cleanup.FolderLibrary.Where(fl => _libraryIds.Contains(fl.LibraryId))
        );
        cleanup.Folders.RemoveRange(cleanup.Folders.Where(f => _folderIds.Contains(f.Id)));
        cleanup.Libraries.RemoveRange(cleanup.Libraries.Where(l => _libraryIds.Contains(l.Id)));
        cleanup.SaveChanges();
        cleanup.Drivers.RemoveRange(cleanup.Drivers.Where(d => _driverIds.Contains(d.Id)));
        cleanup.SaveChanges();
    }

    private (Library Library, Folder Folder) SeedLibraryWithFolder(
        string libraryType,
        string folderPath
    )
    {
        Ulid libraryId = Ulid.NewUlid();
        _libraryIds.Add(libraryId);

        Driver driver = new()
        {
            Id = Ulid.NewUlid(),
            Name = $"test-driver-{Ulid.NewUlid()}",
            Type = "local",
        };
        _driverIds.Add(driver.Id);

        Folder folder = new()
        {
            Id = Ulid.NewUlid(),
            Path = folderPath,
            DriverId = driver.Id,
        };
        _folderIds.Add(folder.Id);

        Library library = new()
        {
            Id = libraryId,
            Title = "Test Library",
            Type = libraryType,
        };

        using MediaContext context = new();
        context.Drivers.Add(driver);
        context.Libraries.Add(library);
        context.Folders.Add(folder);
        context.FolderLibrary.Add(new(folder.Id, libraryId));
        context.SaveChanges();

        return (library, folder);
    }

    private static FileManager BuildManager(Mock<IFileRepository> repoMock, IStorageFactory factory)
    {
        Mock<IStorageDriver> driverMock = new();
        Mock<IMediaAnalyzer> mediaAnalyzerMock = new();
        return new(
            repoMock.Object,
            factory,
            driverMock.Object,
            mediaAnalyzerMock.Object,
            TestFilenameParser.Default
        );
    }

    [Fact]
    public async Task MoveToLibraryFolder_MovieFolderMissingEverywhere_LogsAndReturnsWithoutThrowing()
    {
        (Library sourceLibrary, Folder sourceFolder) = SeedLibraryWithFolder(
            MediaTypes.MovieMediaType,
            Path.Combine(_tempRoot, "source-empty")
        );
        Directory.CreateDirectory(sourceFolder.Path);

        int movieId = 500_001;
        _movieIds.Add(movieId);
        Movie movie = new()
        {
            Id = movieId,
            Title = "Ghost Movie",
            Folder = "Never.On.Disk.2020",
            LibraryId = sourceLibrary.Id,
        };
        using (MediaContext seed = new())
        {
            seed.Movies.Add(movie);
            seed.SaveChanges();
        }

        Mock<IStorageFactory> factoryMock = new();
        LocalStorageDriver driver = new();
        factoryMock
            .Setup(f => f.For(sourceFolder.Id, sourceFolder.DriverId, string.Empty))
            .Returns(new LocalStorage(driver, new StoragePathGuard([], driver)));

        Mock<IFileRepository> repoMock = new();
        FileManager manager = BuildManager(repoMock, factoryMock.Object);

        (Library destLibrary, Folder destFolder) = SeedLibraryWithFolder(
            MediaTypes.MovieMediaType,
            Path.Combine(_tempRoot, "dest-unused")
        );

        Func<Task> act = () => manager.MoveToLibraryFolder(movieId, destFolder);

        await act.Should()
            .NotThrowAsync("a folder that resolves nowhere must log and return, never throw");

        using MediaContext verify = new();
        Movie? unchanged = await verify.Movies.FirstOrDefaultAsync(m => m.Id == movieId);
        unchanged!
            .LibraryId.Should()
            .Be(sourceLibrary.Id, "no folder found means no library move happens");
    }

    [Fact]
    public async Task MoveToLibraryFolder_MovieFound_MovesFilesAndRepointsLibraryAndLibraryMovie()
    {
        (Library sourceLibrary, Folder sourceFolder) = SeedLibraryWithFolder(
            MediaTypes.MovieMediaType,
            Path.Combine(_tempRoot, "source-lib")
        );
        (Library destLibrary, Folder destFolder) = SeedLibraryWithFolder(
            MediaTypes.MovieMediaType,
            Path.Combine(_tempRoot, "dest-lib")
        );
        // Directory.Move (the same-backend fast path) requires the
        // destination's PARENT to already exist — the configured library
        // root, in production terms.
        Directory.CreateDirectory(destFolder.Path);

        string movieFolderName = "My.Movie.2020";
        string sourceMovieDir = Path.Combine(sourceFolder.Path, movieFolderName);
        Directory.CreateDirectory(sourceMovieDir);
        // A non-media file: keeps the move meaningfully non-empty without
        // MediaScan's post-move rescan invoking the real ffprobe binary
        // (info.nfo doesn't match any of MediaScan's extension filters).
        File.WriteAllText(Path.Combine(sourceMovieDir, "info.nfo"), "movie info");

        int movieId = 500_002;
        _movieIds.Add(movieId);
        Movie movie = new()
        {
            Id = movieId,
            Title = "Real Movie",
            Folder = movieFolderName,
            LibraryId = sourceLibrary.Id,
        };
        using (MediaContext seed = new())
        {
            seed.Movies.Add(movie);
            seed.LibraryMovie.Add(new(sourceLibrary.Id, movieId));
            seed.SaveChanges();
        }

        LocalStorageDriver sourceDriver = new();
        LocalStorageDriver destDriver = new();
        IStorage sourceStorage = new LocalStorage(
            sourceDriver,
            new StoragePathGuard([], sourceDriver)
        );
        IStorage destStorage = new LocalStorage(destDriver, new StoragePathGuard([], destDriver));

        Mock<IStorageFactory> factoryMock = new();
        factoryMock
            .Setup(f => f.For(sourceFolder.Id, sourceFolder.DriverId, string.Empty))
            .Returns(sourceStorage);
        factoryMock
            .Setup(f => f.For(destFolder.Id, destFolder.DriverId, string.Empty))
            .Returns(destStorage);

        Mock<IFileRepository> repoMock = new();
        repoMock
            .Setup(r => r.MediaType(movieId, It.IsAny<Library>()))
            .ReturnsAsync((movie, (Tv?)null, MediaTypes.MovieMediaType));

        FileManager manager = BuildManager(repoMock, factoryMock.Object);

        await manager.MoveToLibraryFolder(movieId, destFolder);

        Directory
            .Exists(sourceMovieDir)
            .Should()
            .BeFalse("the source folder must be gone after a same-backend move");
        string destMovieDir = Path.Combine(destFolder.Path, movieFolderName);
        Directory
            .Exists(destMovieDir)
            .Should()
            .BeTrue("the folder must exist under the new library root");
        File.Exists(Path.Combine(destMovieDir, "info.nfo")).Should().BeTrue();

        using MediaContext verify = new();
        Movie? moved = await verify.Movies.FirstOrDefaultAsync(m => m.Id == movieId);
        moved!
            .LibraryId.Should()
            .Be(destLibrary.Id, "the movie must be repointed at the destination library");
        moved.Folder.Should().Be(movieFolderName);

        LibraryMovie? libraryMovie = await verify.LibraryMovie.FirstOrDefaultAsync(lm =>
            lm.MovieId == movieId
        );
        libraryMovie.Should().NotBeNull();
        libraryMovie!
            .LibraryId.Should()
            .Be(destLibrary.Id, "the LibraryMovie link must follow the move too");
    }

    [Fact]
    public async Task MoveToLibraryFolder_TvFound_MovesFilesAndRepointsLibraryAndLibraryTv()
    {
        (Library sourceLibrary, Folder sourceFolder) = SeedLibraryWithFolder(
            MediaTypes.TvMediaType,
            Path.Combine(_tempRoot, "source-tv-lib")
        );
        (Library destLibrary, Folder destFolder) = SeedLibraryWithFolder(
            MediaTypes.TvMediaType,
            Path.Combine(_tempRoot, "dest-tv-lib")
        );
        Directory.CreateDirectory(destFolder.Path);

        string showFolderName = "My.Tv.Show";
        string sourceShowDir = Path.Combine(sourceFolder.Path, showFolderName);
        Directory.CreateDirectory(sourceShowDir);
        File.WriteAllText(Path.Combine(sourceShowDir, "info.nfo"), "show info");

        int tvId = 500_003;
        _tvIds.Add(tvId);
        Tv show = new()
        {
            Id = tvId,
            Title = "Real Show",
            Folder = showFolderName,
            LibraryId = sourceLibrary.Id,
        };
        using (MediaContext seed = new())
        {
            seed.Tvs.Add(show);
            seed.LibraryTv.Add(new(sourceLibrary.Id, tvId));
            seed.SaveChanges();
        }

        LocalStorageDriver sourceDriver = new();
        LocalStorageDriver destDriver = new();
        IStorage sourceStorage = new LocalStorage(
            sourceDriver,
            new StoragePathGuard([], sourceDriver)
        );
        IStorage destStorage = new LocalStorage(destDriver, new StoragePathGuard([], destDriver));

        Mock<IStorageFactory> factoryMock = new();
        factoryMock
            .Setup(f => f.For(sourceFolder.Id, sourceFolder.DriverId, string.Empty))
            .Returns(sourceStorage);
        factoryMock
            .Setup(f => f.For(destFolder.Id, destFolder.DriverId, string.Empty))
            .Returns(destStorage);

        Mock<IFileRepository> repoMock = new();
        repoMock
            .Setup(r => r.MediaType(tvId, It.IsAny<Library>()))
            .ReturnsAsync(((Movie?)null, show, MediaTypes.TvMediaType));

        FileManager manager = BuildManager(repoMock, factoryMock.Object);

        await manager.MoveToLibraryFolder(tvId, destFolder);

        Directory.Exists(sourceShowDir).Should().BeFalse();
        string destShowDir = Path.Combine(destFolder.Path, showFolderName);
        Directory.Exists(destShowDir).Should().BeTrue();

        using MediaContext verify = new();
        Tv? moved = await verify.Tvs.FirstOrDefaultAsync(t => t.Id == tvId);
        moved!.LibraryId.Should().Be(destLibrary.Id);

        LibraryTv? libraryTv = await verify.LibraryTv.FirstOrDefaultAsync(lt => lt.TvId == tvId);
        libraryTv.Should().NotBeNull();
        libraryTv!.LibraryId.Should().Be(destLibrary.Id);
    }
}
