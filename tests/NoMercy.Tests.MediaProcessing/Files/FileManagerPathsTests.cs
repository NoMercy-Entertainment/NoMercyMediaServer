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
// FileManager.Paths() resolves a movie/show's on-disk folder against every
// root Folder scoped to its Library. This is the entry point that decides
// whether a rescan finds ANY files at all — a wrong resolution here silently
// empties a title's whole file set (the FindFiles "preserving existing
// records" guard exists precisely because Paths() can legitimately come back
// empty on a transient backend hiccup). These tests seed a real MediaContext
// (the same file-backed database FileManager.Paths() opens internally via
// `new MediaContext()` — per the job-system convention, it is not injectable)
// so the FolderLibrary scoping query itself is exercised, not mocked away.
// ---------------------------------------------------------------------------
[Trait(name: "Category", value: "Unit")]
public sealed class FileManagerPathsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly List<Ulid> _libraryIds = [];
    private readonly List<Ulid> _folderIds = [];
    private readonly List<Ulid> _driverIds = [];

    public FileManagerPathsTests()
    {
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-paths-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempRoot))
            Directory.Delete(path: _tempRoot, recursive: true);

        using MediaContext cleanup = new();
        cleanup.FolderLibrary.RemoveRange(
            entities: cleanup.FolderLibrary.Where(predicate: fl => _libraryIds.Contains(fl.LibraryId))
        );
        cleanup.Folders.RemoveRange(entities: cleanup.Folders.Where(predicate: f => _folderIds.Contains(f.Id)));
        cleanup.Libraries.RemoveRange(entities: cleanup.Libraries.Where(predicate: l => _libraryIds.Contains(l.Id)));
        cleanup.SaveChanges();
        cleanup.Drivers.RemoveRange(entities: cleanup.Drivers.Where(predicate: d => _driverIds.Contains(d.Id)));
        cleanup.SaveChanges();
    }

    private static FileManager BuildManager(
        Mock<IStorageFactory>? factoryMock = null,
        Mock<IStorageDriver>? driverMock = null
    )
    {
        Mock<IFileRepository> repoMock = new();
        Mock<IMediaAnalyzer> mediaAnalyzerMock = new();
        return new(
            fileRepository: repoMock.Object,
            storageFactory: (factoryMock ?? new()).Object,
            storageDriver: (driverMock ?? new()).Object,
            mediaAnalyzer: mediaAnalyzerMock.Object
        );
    }

    private static List<Folder> InvokePaths(
        FileManager manager,
        Library library,
        Movie? movie,
        Tv? show
    )
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(name: "Paths", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(message: "Paths not found");

        return (List<Folder>)method.Invoke(obj: manager, parameters: [library, movie, show])!;
    }

    private (Library Library, Folder Folder) SeedLibraryWithFolder(
        string libraryType,
        string folderPath
    )
    {
        Ulid libraryId = Ulid.NewUlid();
        _libraryIds.Add(item: libraryId);

        Library library = new()
        {
            Id = libraryId,
            Title = "Test Library",
            Type = libraryType,
        };

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

        using MediaContext context = new();
        context.Drivers.Add(entity: driver);
        context.Libraries.Add(entity: library);
        context.Folders.Add(entity: folder);
        context.FolderLibrary.Add(entity: new(folderId: folder.Id, libraryId: library.Id));
        context.SaveChanges();

        return (library, folder);
    }

    // -----------------------------------------------------------------------
    // folder == null → returns immediately, no DB round trip needed to prove
    // it (a movie with no db row at all would throw on the FolderLibrary
    // query if this guard were missing).
    // -----------------------------------------------------------------------

    [Fact]
    public void Paths_MovieWithNullFolder_ReturnsEmptyWithoutQueryingLibraries()
    {
        FileManager manager = BuildManager();
        Library library = new() { Id = Ulid.NewUlid(), Type = MediaTypes.MovieMediaType };
        Movie movie = new() { Id = 1, Folder = null };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: movie, show: null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Paths_TvWithNullFolder_ReturnsEmpty()
    {
        FileManager manager = BuildManager();
        Library library = new() { Id = Ulid.NewUlid(), Type = MediaTypes.TvMediaType };
        Tv show = new() { Id = 1, Folder = null };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: null, show: show);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Paths_TvLibrary_NoShowAtAll_ShortCircuitsOnNullShow_ReturnsEmpty()
    {
        // Distinct from Paths_TvWithNullFolder_ReturnsEmpty: there, show is a
        // real object with a null Folder. Here show itself is null — the
        // orphaned-id case where MediaType() found no Tv row at all — and
        // `show?.Folder?.Replace(...)` must short-circuit on the FIRST null
        // check without ever touching `.Folder`.
        FileManager manager = BuildManager();
        Library library = new() { Id = Ulid.NewUlid(), Type = MediaTypes.TvMediaType };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: null, show: null);

        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Unknown library type (e.g. music) resolves an empty-string folder — not
    // null — so the DB scoping query still runs, just against an empty
    // sub-path. Exercises the `_ => ""` branch of the folder switch.
    // -----------------------------------------------------------------------

    [Fact]
    public void Paths_UnknownLibraryType_ResolvesEmptyFolder_AndStillQueriesScopedFolders()
    {
        (Library library, Folder rootFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MusicMediaType,
            folderPath: _tempRoot
        );

        Mock<IStorageFactory> factoryMock = new();
        LocalStorageDriver driver = new();
        IStorage storage = new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        factoryMock
            .Setup(expression: f => f.For(rootFolder.Id, rootFolder.DriverId, string.Empty))
            .Returns(value: storage);

        FileManager manager = BuildManager(factoryMock: factoryMock);

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: null, show: null);

        // The scope root itself always "exists" (it's the temp dir this test
        // created), so the empty-string folder resolves to the root and is
        // returned — proving the query executed and scoped to this library.
        result.Should().ContainSingle(predicate: f => f.Id == rootFolder.Id);
    }

    // -----------------------------------------------------------------------
    // Scoping: a FolderLibrary row for a DIFFERENT library must never be
    // probed. Regression guard for the "grabbed every FolderLibrary row
    // system-wide" bug described in the source comment.
    // -----------------------------------------------------------------------

    [Fact]
    public void Paths_OnlyProbesFoldersScopedToTheRequestedLibrary()
    {
        (Library library, Folder rootFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: _tempRoot
        );

        // A second, unrelated library/folder pair whose storage would throw
        // if it were ever touched — proving cross-library isolation.
        Ulid otherLibraryId = Ulid.NewUlid();
        _libraryIds.Add(item: otherLibraryId);
        Ulid otherDriverId = Ulid.NewUlid();
        _driverIds.Add(item: otherDriverId);
        Folder otherFolder = new()
        {
            Id = Ulid.NewUlid(),
            Path = "/should/never/be/probed",
            DriverId = otherDriverId,
        };
        _folderIds.Add(item: otherFolder.Id);
        using (MediaContext seed = new())
        {
            seed.Drivers.Add(
                entity: new()
                {
                    Id = otherDriverId,
                    Name = $"other-driver-{otherDriverId}",
                    Type = "local",
                }
            );
            seed.Libraries.Add(
                entity: new()
                {
                    Id = otherLibraryId,
                    Title = "Other Library",
                    Type = MediaTypes.MovieMediaType,
                }
            );
            seed.Folders.Add(entity: otherFolder);
            seed.FolderLibrary.Add(entity: new(folderId: otherFolder.Id, libraryId: otherLibraryId));
            seed.SaveChanges();
        }

        Mock<IStorageFactory> factoryMock = new();
        LocalStorageDriver driver = new();
        IStorage storage = new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        factoryMock
            .Setup(expression: f => f.For(rootFolder.Id, rootFolder.DriverId, string.Empty))
            .Returns(value: storage);
        factoryMock
            .Setup(expression: f => f.For(otherFolder.Id, otherFolder.DriverId, string.Empty))
            .Throws(exception: new InvalidOperationException(message: "otherFolder must never be resolved"));

        string movieFolderName = "My.Movie.2020";
        Directory.CreateDirectory(path: Path.Combine(path1: _tempRoot, path2: movieFolderName));

        FileManager manager = BuildManager(factoryMock: factoryMock);
        Movie movie = new() { Id = 42, Folder = movieFolderName };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: movie, show: null);

        result.Should().ContainSingle(predicate: f => f.Id == rootFolder.Id);
    }

    // -----------------------------------------------------------------------
    // Exact on-disk match — the common case.
    // -----------------------------------------------------------------------

    [Fact]
    public void Paths_ExactDirectoryExists_ReturnsResolvedFolder()
    {
        (Library library, Folder rootFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: _tempRoot
        );

        string movieFolderName = "My.Movie.2020";
        Directory.CreateDirectory(path: Path.Combine(path1: _tempRoot, path2: movieFolderName));

        Mock<IStorageFactory> factoryMock = new();
        LocalStorageDriver driver = new();
        IStorage storage = new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        factoryMock
            .Setup(expression: f => f.For(rootFolder.Id, rootFolder.DriverId, string.Empty))
            .Returns(value: storage);

        FileManager manager = BuildManager(factoryMock: factoryMock);
        Movie movie = new() { Id = 42, Folder = movieFolderName };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: movie, show: null);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Id.Should().Be(expected: rootFolder.Id);
        result[index: 0].DriverId.Should().Be(expected: rootFolder.DriverId);
        result[index: 0]
            .Path.Should()
            .Be(expected: storage.CombinePath(parent: rootFolder.Path, child: movieFolderName).Replace(oldChar: '\\', newChar: '/'));
    }

    // -----------------------------------------------------------------------
    // Fuzzy fallback: exact path missing, but a punctuation-different
    // directory on disk normalizes to the same name. Exercises
    // TryFindMatchingDirectory's success path plus ResolveBackendPath's
    // LocalStorageDriver branch (storage.GetFullPath, not driver.GetFullPath).
    // -----------------------------------------------------------------------

    [Fact]
    public void Paths_ExactMissing_FuzzyMatchOnDisk_ResolvesToTheMatchedDirectory()
    {
        (Library library, Folder rootFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: _tempRoot
        );

        // Disk has punctuation-different casing of the same title; the DB
        // record's Folder value normalizes identically once compared via
        // FileNameSanitizer.NormalizeForComparison (strips non-alphanumerics).
        string onDiskName = "My Movie (2020)";
        Directory.CreateDirectory(path: Path.Combine(path1: _tempRoot, path2: onDiskName));

        Mock<IStorageFactory> factoryMock = new();
        LocalStorageDriver driver = new();
        IStorage storage = new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        factoryMock
            .Setup(expression: f => f.For(rootFolder.Id, rootFolder.DriverId, string.Empty))
            .Returns(value: storage);

        FileManager manager = BuildManager(factoryMock: factoryMock);
        Movie movie = new() { Id = 42, Folder = "My.Movie.2020" };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: movie, show: null);

        result.Should().HaveCount(expected: 1);
        result[index: 0]
            .Path.Should()
            .Be(expected: storage.CombinePath(parent: rootFolder.Path, child: onDiskName).Replace(oldChar: '\\', newChar: '/'));
    }

    // -----------------------------------------------------------------------
    // No match at all — folder is skipped, not added, no exception.
    // -----------------------------------------------------------------------

    [Fact]
    public void Paths_NoExactAndNoFuzzyMatch_FolderIsSkipped()
    {
        (Library library, Folder rootFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: _tempRoot
        );
        // Root exists but is empty — no candidate directory at all.

        Mock<IStorageFactory> factoryMock = new();
        LocalStorageDriver driver = new();
        IStorage storage = new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        factoryMock
            .Setup(expression: f => f.For(rootFolder.Id, rootFolder.DriverId, string.Empty))
            .Returns(value: storage);

        FileManager manager = BuildManager(factoryMock: factoryMock);
        Movie movie = new() { Id = 42, Folder = "Nothing.Here.2020" };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: movie, show: null);

        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // TvShow uses show.Folder (not movie.Folder) — Anime is routed through
    // the same branch as Tv.
    // -----------------------------------------------------------------------

    [Fact]
    public void Paths_AnimeLibrary_UsesShowFolder()
    {
        (Library library, Folder rootFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.AnimeMediaType,
            folderPath: _tempRoot
        );
        string showFolderName = "My.Anime.Show";
        Directory.CreateDirectory(path: Path.Combine(path1: _tempRoot, path2: showFolderName));

        Mock<IStorageFactory> factoryMock = new();
        LocalStorageDriver driver = new();
        IStorage storage = new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        factoryMock
            .Setup(expression: f => f.For(rootFolder.Id, rootFolder.DriverId, string.Empty))
            .Returns(value: storage);

        FileManager manager = BuildManager(factoryMock: factoryMock);
        Tv show = new() { Id = 7, Folder = showFolderName };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: null, show: show);

        result.Should().HaveCount(expected: 1);
        result[index: 0]
            .Path.Should()
            .Be(expected: storage.CombinePath(parent: rootFolder.Path, child: showFolderName).Replace(oldChar: '\\', newChar: '/'));
    }

    [Fact]
    public void Paths_TvLibrary_UsesShowFolder()
    {
        // The switch arm `TvMediaType or AnimeMediaType` is one pattern with
        // two matchable cases — Paths_AnimeLibrary_UsesShowFolder above only
        // exercises the Anime half. This pins the Tv half with a real
        // (non-null) folder, distinct from Paths_TvWithNullFolder_ReturnsEmpty
        // which never reaches the DB query at all.
        (Library library, Folder rootFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.TvMediaType,
            folderPath: _tempRoot
        );
        string showFolderName = "My.Tv.Show";
        Directory.CreateDirectory(path: Path.Combine(path1: _tempRoot, path2: showFolderName));

        Mock<IStorageFactory> factoryMock = new();
        LocalStorageDriver driver = new();
        IStorage storage = new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
        factoryMock
            .Setup(expression: f => f.For(rootFolder.Id, rootFolder.DriverId, string.Empty))
            .Returns(value: storage);

        FileManager manager = BuildManager(factoryMock: factoryMock);
        Tv show = new() { Id = 8, Folder = showFolderName };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: null, show: show);

        result.Should().HaveCount(expected: 1);
        result[index: 0]
            .Path.Should()
            .Be(expected: storage.CombinePath(parent: rootFolder.Path, child: showFolderName).Replace(oldChar: '\\', newChar: '/'));
    }

    // -----------------------------------------------------------------------
    // A remote backend that throws on Exists() (and again on the fuzzy-match
    // fallback's DirectoryExists()) must be treated as "not in this folder",
    // never allowed to blow up the whole rescan. Covers TryExists' catch
    // branch, TryFindMatchingDirectory's catch branch, and
    // ResolveBackendPath's non-LocalStorageDriver branch in one pass — this
    // is exactly the "one flaky remote backend" scenario the source comment
    // documents. Mocking here targets the EXTERNAL backend, not the unit
    // under test.
    // -----------------------------------------------------------------------

    private sealed class ThrowingRemoteDriver : IStorageDriver
    {
        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) =>
            throw new IOException(message: "simulated remote transport failure");

        public void CreateDirectory(string path) { }

        public void DeleteFile(string path) { }

        public void DeleteDirectory(string path, bool recursive) { }

        public long GetFileSize(string path) => 0;

        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;

        public DateTime GetCreationTimeUtc(string path) => DateTime.UtcNow;

        public DateTime GetLastAccessTimeUtc(string path) => DateTime.UtcNow;

        public Stream OpenRead(string path) => Stream.Null;

        public Stream OpenWrite(string path, bool overwrite) => Stream.Null;

        public void MoveFile(string source, string destination) { }

        public void CopyFile(string source, string destination, bool overwrite) { }

        public void MoveDirectory(string source, string destination) { }

        public IEnumerable<string> EnumerateFileSystemEntries(
            string directory,
            string searchPattern,
            SearchOption option
        ) => [];

        public string GetFullPath(string path) => path;

        public string? ResolveLinkTarget(string path) => null;

        public bool IsHidden(string path) => false;
    }

    [Fact]
    public void Paths_RemoteBackendThrowsOnExistsAndOnFuzzyMatch_FolderSkippedNotThrown()
    {
        (Library library, Folder rootFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: "/remote/export/root"
        );

        ThrowingRemoteDriver remoteDriver = new();
        Mock<IStorage> remoteStorage = new();
        remoteStorage.Setup(expression: s => s.Driver).Returns(value: remoteDriver);
        remoteStorage
            .Setup(expression: s => s.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(valueFunction: (string p, string c) => $"{p.TrimEnd(trimChar: '/')}/{c}");
        remoteStorage
            .Setup(expression: s => s.Exists(It.IsAny<string>()))
            .Throws(exception: new IOException(message: "simulated remote transport failure"));

        Mock<IStorageFactory> factoryMock = new();
        factoryMock
            .Setup(expression: f => f.For(rootFolder.Id, rootFolder.DriverId, string.Empty))
            .Returns(value: remoteStorage.Object);

        FileManager manager = BuildManager(factoryMock: factoryMock);
        Movie movie = new() { Id = 42, Folder = "Some.Movie.2020" };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: movie, show: null);

        result
            .Should()
            .BeEmpty(because: "a transport failure on one backend must not throw or add a bogus folder");
    }

    // -----------------------------------------------------------------------
    // Non-local backend, no exceptions: ResolveBackendPath must call
    // driver.GetFullPath (not storage.GetFullPath, which is the
    // LocalStorage-only escape hatch) and the fuzzy match must still resolve
    // through the raw driver.
    // -----------------------------------------------------------------------

    private sealed class FuzzyMatchRemoteDriver(string matchDirectory) : IStorageDriver
    {
        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) => true;

        public void CreateDirectory(string path) { }

        public void DeleteFile(string path) { }

        public void DeleteDirectory(string path, bool recursive) { }

        public long GetFileSize(string path) => 0;

        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;

        public DateTime GetCreationTimeUtc(string path) => DateTime.UtcNow;

        public DateTime GetLastAccessTimeUtc(string path) => DateTime.UtcNow;

        public Stream OpenRead(string path) => Stream.Null;

        public Stream OpenWrite(string path, bool overwrite) => Stream.Null;

        public void MoveFile(string source, string destination) { }

        public void CopyFile(string source, string destination, bool overwrite) { }

        public void MoveDirectory(string source, string destination) { }

        public IEnumerable<string> EnumerateFileSystemEntries(
            string directory,
            string searchPattern,
            SearchOption option
        ) => [matchDirectory];

        public string GetFullPath(string path) => $"/resolved{path}";

        public string? ResolveLinkTarget(string path) => null;

        public bool IsHidden(string path) => false;
    }

    [Fact]
    public void Paths_NonLocalBackend_FuzzyMatchResolvesThroughDriverGetFullPath()
    {
        (Library library, Folder rootFolder) = SeedLibraryWithFolder(
            libraryType: MediaTypes.MovieMediaType,
            folderPath: "export/root"
        );

        FuzzyMatchRemoteDriver remoteDriver = new(matchDirectory: "export/root/My Movie (2020)");
        Mock<IStorage> remoteStorage = new();
        remoteStorage.Setup(expression: s => s.Driver).Returns(value: remoteDriver);
        remoteStorage
            .Setup(expression: s => s.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(valueFunction: (string p, string c) => $"{p.TrimEnd(trimChar: '/')}/{c}");
        remoteStorage.Setup(expression: s => s.Exists("export/root/My.Movie.2020")).Returns(value: false);
        remoteStorage.Setup(expression: s => s.Exists("export/root/My Movie (2020)")).Returns(value: true);
        remoteStorage
            .Setup(expression: s => s.GetName("export/root/My Movie (2020)"))
            .Returns(value: "My Movie (2020)");

        Mock<IStorageFactory> factoryMock = new();
        factoryMock
            .Setup(expression: f => f.For(rootFolder.Id, rootFolder.DriverId, string.Empty))
            .Returns(value: remoteStorage.Object);

        FileManager manager = BuildManager(factoryMock: factoryMock);
        Movie movie = new() { Id = 42, Folder = "My.Movie.2020" };

        List<Folder> result = InvokePaths(manager: manager, library: library, movie: movie, show: null);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Path.Should().Be(expected: "export/root/My Movie (2020)");
    }
}
