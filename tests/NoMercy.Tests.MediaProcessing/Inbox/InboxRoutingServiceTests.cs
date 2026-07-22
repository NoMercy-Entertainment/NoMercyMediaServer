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
using NoMercy.Database.Models.Media;
using NoMercy.MediaProcessing.Inbox;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.Storage;

namespace NoMercy.Tests.MediaProcessing.Inbox;

[Trait(name: "Category", value: "Unit")]
public class InboxRoutingServiceTests : IDisposable
{
    // -----------------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------------

    private readonly MediaContext _context;
    private readonly Mock<IStorageFactory> _storageFactoryMock;
    private readonly Mock<IStorage> _storageMock;
    private readonly Mock<IStorageDriver> _driverMock;
    private readonly Mock<JobDispatcher> _dispatcherMock;

    public InboxRoutingServiceTests()
    {
        DbContextOptionsBuilder<MediaContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");

        _context = new(options: optionsBuilder.Options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        _context.Database.ExecuteSqlRaw(sql: "PRAGMA foreign_keys = OFF;");

        _driverMock = new();
        _driverMock.Setup(expression: d => d.GetFullPath(It.IsAny<string>())).Returns<string>(valueFunction: p => p);
        _driverMock.Setup(expression: d => d.MoveFile(It.IsAny<string>(), It.IsAny<string>()));
        _driverMock.Setup(expression: d => d.CreateDirectory(It.IsAny<string>()));
        _driverMock.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: true);

        _storageMock = new();
        _storageMock.Setup(expression: s => s.Driver).Returns(value: _driverMock.Object);
        _storageMock.Setup(expression: s => s.GetFullPath(It.IsAny<string>())).Returns<string>(valueFunction: p => p);
        _storageMock
            .Setup(expression: s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: []);
        _storageMock
            .Setup(expression: s =>
                s.WriteAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())
            )
            .Returns(value: Task.CompletedTask);
        _storageMock
            .Setup(expression: s => s.SizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: 0L);
        _storageMock
            .Setup(expression: s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);

        _storageFactoryMock = new();
        _storageFactoryMock
            .Setup(expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
            .Returns(value: _storageMock.Object);

        _dispatcherMock = new();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    private InboxRoutingService MakeService() =>
        new(storageFactory: _storageFactoryMock.Object, jobDispatcher: _dispatcherMock.Object);

    // -----------------------------------------------------------------------
    // Seed helpers
    // -----------------------------------------------------------------------

    private (Library library, Folder folder, Ulid profileId) SeedLibraryWithProfile(string type)
    {
        Ulid driverId = Ulid.NewUlid();
        Ulid profileId = Ulid.NewUlid();

        Library library = new()
        {
            Id = Ulid.NewUlid(),
            Title = $"{type} library",
            Type = type,
        };

        Folder folder = new()
        {
            Id = Ulid.NewUlid(),
            Path = $"/media/{type}",
            DriverId = driverId,
        };

        EncodingPresetFolder profileFolder = new()
        {
            PresetId = profileId,
            FolderId = folder.Id,
            IsDefault = true,
        };

        FolderLibrary folderLibrary = new() { FolderId = folder.Id, LibraryId = library.Id };

        _context.Libraries.Add(entity: library);
        _context.Folders.Add(entity: folder);
        _context.EncodingPresetFolders.Add(entity: profileFolder);
        _context.FolderLibrary.Add(entity: folderLibrary);
        _context.SaveChanges();

        return (library, folder, profileId);
    }

    private Folder SeedFolderWithoutProfile(Library library)
    {
        Ulid driverId = Ulid.NewUlid();

        Folder folder = new()
        {
            Id = Ulid.NewUlid(),
            Path = "/media/noprofile",
            DriverId = driverId,
        };

        FolderLibrary folderLibrary = new() { FolderId = folder.Id, LibraryId = library.Id };

        _context.Folders.Add(entity: folder);
        _context.FolderLibrary.Add(entity: folderLibrary);
        _context.SaveChanges();

        return folder;
    }

    private static CandidateMatch MakeCandidate(string provider, string externalId, string title) =>
        new()
        {
            Provider = provider,
            ExternalId = externalId,
            Title = title,
            Year = 2020,
            Score = 0.95,
        };

    private static ClassificationResult MakeClassification(
        string detectedType,
        string confidence,
        CandidateMatch[]? candidates = null
    ) =>
        new()
        {
            DetectedType = detectedType,
            Confidence = confidence,
            Candidates = candidates ?? [MakeCandidate(provider: "tmdb", externalId: "12345", title: "Test Title")],
        };

    // -----------------------------------------------------------------------
    // ResolveDestinations
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveDestinations_OneValidMovieDestination_ReturnsOne()
    {
        SeedLibraryWithProfile(type: "movie");
        InboxRoutingService service = MakeService();

        List<InboxDestination> results = await service.ResolveDestinations(detectedType: "movie", context: _context);

        results.Should().HaveCount(expected: 1);
    }

    [Fact]
    public async Task ResolveDestinations_FolderWithoutProfile_IsIncluded()
    {
        Library library = new()
        {
            Id = Ulid.NewUlid(),
            Title = "movie library no profile",
            Type = "movie",
        };
        _context.Libraries.Add(entity: library);
        _context.SaveChanges();

        Folder folder = SeedFolderWithoutProfile(library: library);

        InboxRoutingService service = MakeService();
        List<InboxDestination> results = await service.ResolveDestinations(detectedType: "movie", context: _context);

        results.Should().HaveCount(expected: 1);
        results[index: 0].LibraryId.Should().Be(expected: library.Id);
        results[index: 0].FolderId.Should().Be(expected: folder.Id);
        results[index: 0].ProfileId.Should().Be(expected: Ulid.Empty);
    }

    [Fact]
    public async Task ResolveDestinations_TypeMismatchedLibrary_IsExcluded()
    {
        SeedLibraryWithProfile(type: "tv");

        InboxRoutingService service = MakeService();
        List<InboxDestination> results = await service.ResolveDestinations(detectedType: "movie", context: _context);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveDestinations_TwoValidDestinations_ReturnsBoth()
    {
        SeedLibraryWithProfile(type: "movie");
        SeedLibraryWithProfile(type: "movie");

        InboxRoutingService service = MakeService();
        List<InboxDestination> results = await service.ResolveDestinations(detectedType: "movie", context: _context);

        results.Should().HaveCount(expected: 2);
    }

    [Fact]
    public async Task ResolveDestinations_MapsProfileIdAndDriverId()
    {
        (Library _, Folder folder, Ulid profileId) = SeedLibraryWithProfile(type: "movie");

        InboxRoutingService service = MakeService();
        List<InboxDestination> results = await service.ResolveDestinations(detectedType: "movie", context: _context);

        InboxDestination dest = results.Single();
        dest.ProfileId.Should().Be(expected: profileId);
        dest.DriverId.Should().Be(expected: folder.DriverId);
        dest.FolderId.Should().Be(expected: folder.Id);
    }

    // -----------------------------------------------------------------------
    // Route — auto branch
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Route_HighConfidence_SingleMatch_SingleDestination_ReturnsAuto()
    {
        SeedLibraryWithProfile(type: "movie");
        InboxRoutingService service = MakeService();

        ClassificationResult classification = MakeClassification(detectedType: "movie", confidence: "high");
        RouteOutcome outcome = await service.Route(
            classification: classification,
            sourcePath: "inbox/The Matrix (1999).mkv",
            driverId: Ulid.NewUlid(),
            context: _context
        );

        outcome.Mode.Should().Be(expected: "auto");
        outcome.Destination.Should().NotBeNull();
        outcome.Item.Should().NotBeNull();
        outcome.Item.Status.Should().Be(expected: "Routing");
    }

    // -----------------------------------------------------------------------
    // Route — review branch: zero destinations
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Route_ZeroDestinations_ReturnsReview()
    {
        InboxRoutingService service = MakeService();

        ClassificationResult classification = MakeClassification(detectedType: "movie", confidence: "high");
        RouteOutcome outcome = await service.Route(
            classification: classification,
            sourcePath: "inbox/movie.mkv",
            driverId: Ulid.NewUlid(),
            context: _context
        );

        outcome.Mode.Should().Be(expected: "review");
        outcome.Destination.Should().BeNull();
        outcome.Item.Status.Should().Be(expected: "NeedsReview");
    }

    // -----------------------------------------------------------------------
    // Route — review branch: multiple destinations
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Route_MultipleDestinations_ReturnsReview()
    {
        SeedLibraryWithProfile(type: "movie");
        SeedLibraryWithProfile(type: "movie");

        InboxRoutingService service = MakeService();
        ClassificationResult classification = MakeClassification(detectedType: "movie", confidence: "high");

        RouteOutcome outcome = await service.Route(
            classification: classification,
            sourcePath: "inbox/movie.mkv",
            driverId: Ulid.NewUlid(),
            context: _context
        );

        outcome.Mode.Should().Be(expected: "review");
        outcome.Item.Status.Should().Be(expected: "NeedsReview");
    }

    // -----------------------------------------------------------------------
    // Route — review branch: medium/low confidence
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(data: "medium")]
    [InlineData(data: "low")]
    public async Task Route_NonHighConfidence_ReturnsReview(string confidence)
    {
        SeedLibraryWithProfile(type: "movie");
        InboxRoutingService service = MakeService();

        ClassificationResult classification = MakeClassification(detectedType: "movie", confidence: confidence);
        RouteOutcome outcome = await service.Route(
            classification: classification,
            sourcePath: "inbox/movie.mkv",
            driverId: Ulid.NewUlid(),
            context: _context
        );

        outcome.Mode.Should().Be(expected: "review");
        outcome.Item.Status.Should().Be(expected: "NeedsReview");
    }

    // -----------------------------------------------------------------------
    // Route — InboxItem always populated
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Route_ReviewPath_PopulatesInboxItemWithCandidates()
    {
        InboxRoutingService service = MakeService();

        CandidateMatch candidate = MakeCandidate(provider: "tmdb", externalId: "999", title: "Unknown Film");
        ClassificationResult classification = MakeClassification(detectedType: "movie", confidence: "low", candidates: [candidate]);

        RouteOutcome outcome = await service.Route(
            classification: classification,
            sourcePath: "inbox/unknown.mkv",
            driverId: Ulid.NewUlid(),
            context: _context
        );

        outcome.Item.DetectedType.Should().Be(expected: "movie");
        outcome.Item.Confidence.Should().Be(expected: "low");
        outcome.Item.Candidates.Should().HaveCount(expected: 1);
        outcome.Item.Candidates[0].Title.Should().Be(expected: "Unknown Film");
        outcome.Item.TargetLibraryId.Should().BeNull();
        outcome.Item.TargetFolderId.Should().BeNull();
        outcome.Item.TargetProfileId.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // ExecuteAuto — movie: MoveAsync + MovieImportJob dispatched
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAuto_Movie_CallsMoveAsync_AndDispatchesMovieImportJob()
    {
        (Library library, Folder folder, _) = SeedLibraryWithProfile(type: "movie");

        InboxRoutingService service = MakeService();

        CandidateMatch candidate = MakeCandidate(provider: "tmdb", externalId: "603", title: "The Matrix");
        InboxDestination destination = new()
        {
            LibraryId = library.Id,
            FolderId = folder.Id,
            ProfileId = Ulid.NewUlid(),
            DriverId = folder.DriverId,
            FolderPath = folder.Path,
        };

        InboxItem item = new()
        {
            Id = Ulid.NewUlid(),
            SourcePath = "inbox/The Matrix (1999).mkv",
            DriverId = folder.DriverId,
            DetectedType = "movie",
            Confidence = "high",
            Status = "Routing",
            Candidates = [candidate],
        };

        RouteOutcome outcome = new()
        {
            Mode = "auto",
            Destination = destination,
            Item = item,
        };

        await service.ExecuteAuto(outcome: outcome, context: _context);

        // Same-driver branch: uses Driver.MoveFile with absolute paths, not IStorage.MoveAsync.
        _driverMock.Verify(
            expression: d => d.MoveFile("inbox/The Matrix (1999).mkv", "The Matrix (1999).mkv"),
            times: Times.Once
        );

        _dispatcherMock.Verify(expression: d => d.DispatchJob<MovieImportJob>(603, library.Id), times: Times.Once);

        item.Status.Should().Be(expected: "Imported");
        item.TargetLibraryId.Should().Be(expected: library.Id);
        item.TargetFolderId.Should().Be(expected: folder.Id);
    }

    // -----------------------------------------------------------------------
    // ExecuteAuto — tv: MoveAsync + ShowImportJob dispatched
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAuto_Tv_DispatchesShowImportJob()
    {
        (Library library, Folder folder, _) = SeedLibraryWithProfile(type: "tv");

        InboxRoutingService service = MakeService();

        CandidateMatch candidate = MakeCandidate(provider: "tmdb", externalId: "1396", title: "Breaking Bad");
        InboxDestination destination = new()
        {
            LibraryId = library.Id,
            FolderId = folder.Id,
            ProfileId = Ulid.NewUlid(),
            DriverId = folder.DriverId,
            FolderPath = folder.Path,
        };

        InboxItem item = new()
        {
            Id = Ulid.NewUlid(),
            SourcePath = "inbox/Breaking Bad S01E01.mkv",
            DriverId = folder.DriverId,
            DetectedType = "tv",
            Confidence = "high",
            Status = "Routing",
            Candidates = [candidate],
        };

        RouteOutcome outcome = new()
        {
            Mode = "auto",
            Destination = destination,
            Item = item,
        };

        await service.ExecuteAuto(outcome: outcome, context: _context);

        _dispatcherMock.Verify(expression: d => d.DispatchJob<ShowImportJob>(1396, library.Id), times: Times.Once);

        item.Status.Should().Be(expected: "Imported");
    }

    // -----------------------------------------------------------------------
    // ExecuteAuto — anime: ShowImportJob dispatched
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAuto_Anime_DispatchesShowImportJob()
    {
        (Library library, Folder folder, _) = SeedLibraryWithProfile(type: "anime");

        InboxRoutingService service = MakeService();

        CandidateMatch candidate = MakeCandidate(provider: "tmdb", externalId: "31478", title: "Frieren");
        InboxDestination destination = new()
        {
            LibraryId = library.Id,
            FolderId = folder.Id,
            ProfileId = Ulid.NewUlid(),
            DriverId = folder.DriverId,
            FolderPath = folder.Path,
        };

        InboxItem item = new()
        {
            Id = Ulid.NewUlid(),
            SourcePath = "inbox/Frieren - 01.mkv",
            DriverId = folder.DriverId,
            DetectedType = "anime",
            Confidence = "high",
            Status = "Routing",
            Candidates = [candidate],
        };

        RouteOutcome outcome = new()
        {
            Mode = "auto",
            Destination = destination,
            Item = item,
        };

        await service.ExecuteAuto(outcome: outcome, context: _context);

        _dispatcherMock.Verify(expression: d => d.DispatchJob<ShowImportJob>(31478, library.Id), times: Times.Once);

        item.Status.Should().Be(expected: "Imported");
    }

    // -----------------------------------------------------------------------
    // ExecuteAuto — music: AudioImportJob dispatched with libraryId/folderId/path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAuto_Music_DispatchesAudioImportJob()
    {
        (Library library, Folder folder, _) = SeedLibraryWithProfile(type: "music");

        InboxRoutingService service = MakeService();

        CandidateMatch candidate = MakeCandidate(provider: "musicbrainz", externalId: "some-release-id", title: "Album Name");
        InboxDestination destination = new()
        {
            LibraryId = library.Id,
            FolderId = folder.Id,
            ProfileId = Ulid.NewUlid(),
            DriverId = folder.DriverId,
            FolderPath = "/media/music",
        };

        InboxItem item = new()
        {
            Id = Ulid.NewUlid(),
            SourcePath = "inbox/Artist - Album/01 - Track.flac",
            DriverId = folder.DriverId,
            DetectedType = "music",
            Confidence = "high",
            Status = "Routing",
            Candidates = [candidate],
        };

        RouteOutcome outcome = new()
        {
            Mode = "auto",
            Destination = destination,
            Item = item,
        };

        await service.ExecuteAuto(outcome: outcome, context: _context);

        _dispatcherMock.Verify(
            expression: d => d.DispatchJob<AudioImportJob>(library.Id, folder.Id, It.IsAny<string>()),
            times: Times.Once
        );

        item.Status.Should().Be(expected: "Imported");
    }

    // -----------------------------------------------------------------------
    // ExecuteAuto — VideoEncodeJob is NEVER dispatched
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAuto_Movie_DoesNotDispatchVideoEncodeJob()
    {
        (Library library, Folder folder, _) = SeedLibraryWithProfile(type: "movie");

        InboxRoutingService service = MakeService();

        CandidateMatch candidate = MakeCandidate(provider: "tmdb", externalId: "603", title: "The Matrix");
        InboxDestination destination = new()
        {
            LibraryId = library.Id,
            FolderId = folder.Id,
            ProfileId = Ulid.NewUlid(),
            DriverId = folder.DriverId,
            FolderPath = folder.Path,
        };

        InboxItem item = new()
        {
            Id = Ulid.NewUlid(),
            SourcePath = "inbox/The Matrix (1999).mkv",
            DriverId = folder.DriverId,
            DetectedType = "movie",
            Confidence = "high",
            Status = "Routing",
            Candidates = [candidate],
        };

        RouteOutcome outcome = new()
        {
            Mode = "auto",
            Destination = destination,
            Item = item,
        };

        await service.ExecuteAuto(outcome: outcome, context: _context);

        _dispatcherMock.Verify(
            expression: d =>
                d.DispatchJob<VideoEncodeJob>(
                    It.IsAny<Ulid>(),
                    It.IsAny<Ulid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Ulid?>()
                ),
            times: Times.Never
        );
    }

    // -----------------------------------------------------------------------
    // ExecuteAuto — profile-less destination: TargetProfileId is null, not Ulid.Empty
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAuto_ProfileLessDestination_SetsTargetProfileIdToNull()
    {
        Library library = new()
        {
            Id = Ulid.NewUlid(),
            Title = "movie library no profile",
            Type = "movie",
        };
        _context.Libraries.Add(entity: library);
        _context.SaveChanges();

        Folder folder = SeedFolderWithoutProfile(library: library);

        InboxRoutingService service = MakeService();

        CandidateMatch candidate = MakeCandidate(provider: "tmdb", externalId: "603", title: "The Matrix");
        InboxDestination destination = new()
        {
            LibraryId = library.Id,
            FolderId = folder.Id,
            ProfileId = Ulid.Empty,
            DriverId = folder.DriverId,
            FolderPath = folder.Path,
        };

        InboxItem item = new()
        {
            Id = Ulid.NewUlid(),
            SourcePath = "inbox/The Matrix (1999).mkv",
            DriverId = folder.DriverId,
            DetectedType = "movie",
            Confidence = "high",
            Status = "Routing",
            Candidates = [candidate],
        };

        RouteOutcome outcome = new()
        {
            Mode = "auto",
            Destination = destination,
            Item = item,
        };

        await service.ExecuteAuto(outcome: outcome, context: _context);

        item.TargetProfileId.Should().BeNull();

        InboxItem? persisted = await _context.InboxItems.FindAsync(keyValues: item.Id);
        persisted.Should().NotBeNull();
        persisted!.TargetProfileId.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // ExecuteAuto — status transitions: Routing -> Imported
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAuto_SetsStatusToImported_AfterMove()
    {
        (Library library, Folder folder, _) = SeedLibraryWithProfile(type: "movie");

        InboxRoutingService service = MakeService();

        CandidateMatch candidate = MakeCandidate(provider: "tmdb", externalId: "100", title: "Test Movie");
        InboxDestination destination = new()
        {
            LibraryId = library.Id,
            FolderId = folder.Id,
            ProfileId = Ulid.NewUlid(),
            DriverId = folder.DriverId,
            FolderPath = folder.Path,
        };

        InboxItem item = new()
        {
            Id = Ulid.NewUlid(),
            SourcePath = "inbox/Test Movie (2020).mkv",
            DriverId = folder.DriverId,
            DetectedType = "movie",
            Confidence = "high",
            Status = "Routing",
            Candidates = [candidate],
        };

        RouteOutcome outcome = new()
        {
            Mode = "auto",
            Destination = destination,
            Item = item,
        };

        await service.ExecuteAuto(outcome: outcome, context: _context);

        item.Status.Should().Be(expected: "Imported");

        InboxItem? persisted = await _context.InboxItems.FindAsync(keyValues: item.Id);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(expected: "Imported");
    }
}
