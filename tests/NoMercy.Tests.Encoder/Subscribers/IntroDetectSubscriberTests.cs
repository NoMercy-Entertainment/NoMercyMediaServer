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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.ContentAnalysis.Fingerprinting;
using NoMercy.Encoder.Subscribers;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Storage;
using Xunit;

namespace NoMercy.Tests.Encoder.Subscribers;

public class IntroDetectSubscriberTests
{
    private static IDbContextFactory<MediaContext> InMemoryFactory(string tag)
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase(databaseName: $"intro-detect-{tag}-{Ulid.NewUlid()}")
            .Options;
        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(expression: f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: () => new(options: options));
        mock.Setup(expression: f => f.CreateDbContext()).Returns(valueFunction: () => new(options: options));
        return mock.Object;
    }

    private static IntroDetectSubscriber BuildSubscriber(
        IEventBus bus,
        IDbContextFactory<MediaContext> factory,
        IAudioFingerprinter fingerprinter,
        IIntroDetector detector,
        bool enabled = true
    )
    {
        EncoderOptions options = new() { EnableIntroDetectSubscriber = enabled };
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: true);
        return new(
            eventBus: bus,
            fingerprinter: fingerprinter,
            introDetector: detector,
            options: options,
            logger: NullLogger<IntroDetectSubscriber>.Instance,
            storage: storage.Object,
            contextFactory: factory
        );
    }

    private static async Task SeedTvSeasonAsync(
        IDbContextFactory<MediaContext> factory,
        Ulid libraryId,
        int tvId,
        int seasonId,
        int[] episodeIds,
        string hostFolder = "/media/tv/"
    )
    {
        await using MediaContext ctx = await factory.CreateDbContextAsync();

        ctx.Libraries.Add(
            entity: new()
            {
                Id = libraryId,
                Title = "TV Library",
                Type = "tv",
            }
        );

        ctx.Tvs.Add(
            entity: new()
            {
                Id = tvId,
                Title = "Test Show",
                TitleSort = "testshow",
                LibraryId = libraryId,
            }
        );

        ctx.Seasons.Add(
            entity: new()
            {
                Id = seasonId,
                TvId = tvId,
                SeasonNumber = 1,
                EpisodeCount = episodeIds.Length,
            }
        );

        ctx.LibraryTv.Add(entity: new(libraryId: libraryId, tvId: tvId));

        for (int index = 0; index < episodeIds.Length; index++)
        {
            int episodeId = episodeIds[index];
            ctx.Episodes.Add(
                entity: new()
                {
                    Id = episodeId,
                    TvId = tvId,
                    SeasonId = seasonId,
                    SeasonNumber = 1,
                    EpisodeNumber = index + 1,
                }
            );
            ctx.VideoFiles.Add(
                entity: new()
                {
                    Filename = $"s01e0{index + 1}.mkv",
                    HostFolder = hostFolder,
                    Folder = hostFolder,
                    EpisodeId = episodeId,
                }
            );
        }

        await ctx.SaveChangesAsync();
    }

    private static AudioFingerprint FakeFingerprint(int frameCount = 100) =>
        new(
            Hashes: Enumerable.Range(start: 0, count: frameCount).Select(selector: i => (uint)(i * 17)).ToArray(),
            FrameDuration: TimeSpan.FromMilliseconds(milliseconds: 125),
            StartTime: TimeSpan.Zero
        );

    [Fact]
    public async Task OnLibraryScanCompleted_DisabledViaOptions_WritesNoContentSegments()
    {
        IDbContextFactory<MediaContext> factory = InMemoryFactory(tag: "disabled");
        Mock<IAudioFingerprinter> fingerprinter = new();
        Mock<IIntroDetector> detector = new();
        InMemoryEventBus bus = new();

        using IntroDetectSubscriber subject = BuildSubscriber(
            bus: bus,
            factory: factory,
            fingerprinter: fingerprinter.Object,
            detector: detector.Object,
            enabled: false
        );

        Ulid libraryId = Ulid.NewUlid();
        int tvId = 1;
        int seasonId = 10;
        await SeedTvSeasonAsync(factory: factory, libraryId: libraryId, tvId: tvId, seasonId: seasonId, episodeIds: [100, 101, 102]);

        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = libraryId,
                LibraryName = "TV",
                ItemsFound = 3,
                Duration = TimeSpan.FromSeconds(seconds: 1),
            }
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ContentSegments.Should().BeEmpty(because: "opt-out disables the subscriber");

        fingerprinter.Verify(
            expression: f =>
                f.FingerprintAsync(
                    It.IsAny<string>(),
                    It.IsAny<FingerprintWindow?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task OnLibraryScanCompleted_NoMatchingSeasons_WritesNoSegments()
    {
        IDbContextFactory<MediaContext> factory = InMemoryFactory(tag: "noseasons");
        Mock<IAudioFingerprinter> fingerprinter = new();
        Mock<IIntroDetector> detector = new();
        InMemoryEventBus bus = new();

        using IntroDetectSubscriber subject = BuildSubscriber(
            bus: bus,
            factory: factory,
            fingerprinter: fingerprinter.Object,
            detector: detector.Object
        );

        Ulid unrelatedLibrary = Ulid.NewUlid();

        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = unrelatedLibrary,
                LibraryName = "Empty",
                ItemsFound = 0,
                Duration = TimeSpan.Zero,
            }
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ContentSegments.Should().BeEmpty();
        fingerprinter.Verify(
            expression: f =>
                f.FingerprintAsync(
                    It.IsAny<string>(),
                    It.IsAny<FingerprintWindow?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task OnLibraryScanCompleted_MusicLibrary_DoesNoWork_DoesNotFingerprint()
    {
        IDbContextFactory<MediaContext> factory = InMemoryFactory(tag: "music");
        Mock<IAudioFingerprinter> fingerprinter = new();
        Mock<IIntroDetector> detector = new();
        InMemoryEventBus bus = new();

        using IntroDetectSubscriber subject = BuildSubscriber(
            bus: bus,
            factory: factory,
            fingerprinter: fingerprinter.Object,
            detector: detector.Object
        );

        Ulid musicLibraryId = Ulid.NewUlid();
        await using (MediaContext seedCtx = await factory.CreateDbContextAsync())
        {
            seedCtx.Libraries.Add(
                entity: new()
                {
                    Id = musicLibraryId,
                    Title = "Music",
                    Type = "music",
                }
            );
            await seedCtx.SaveChangesAsync();
        }

        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = musicLibraryId,
                LibraryName = "Music",
                ItemsFound = 0,
                Duration = TimeSpan.Zero,
            }
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ContentSegments.Should()
            .BeEmpty(because: "intro detection is a TV feature and must not run on a music library");
        fingerprinter.Verify(
            expression: f =>
                f.FingerprintAsync(
                    It.IsAny<string>(),
                    It.IsAny<FingerprintWindow?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task OnLibraryScanCompleted_SeasonHasOnlyOneEpisode_SkipsDetection_WritesNoSegments()
    {
        IDbContextFactory<MediaContext> factory = InMemoryFactory(tag: "singleepisode");
        Mock<IAudioFingerprinter> fingerprinter = new();
        Mock<IIntroDetector> detector = new();
        InMemoryEventBus bus = new();

        using IntroDetectSubscriber subject = BuildSubscriber(
            bus: bus,
            factory: factory,
            fingerprinter: fingerprinter.Object,
            detector: detector.Object
        );

        Ulid libraryId = Ulid.NewUlid();
        await SeedTvSeasonAsync(factory: factory, libraryId: libraryId, tvId: 2, seasonId: 20, episodeIds: [200]);

        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = libraryId,
                LibraryName = "TV",
                ItemsFound = 1,
                Duration = TimeSpan.Zero,
            }
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ContentSegments.Should()
            .BeEmpty(because: "fewer than 2 episodes → cannot detect shared segments");
    }

    [Fact]
    public async Task OnLibraryScanCompleted_TwoEpisodes_WithDetectedIntro_WritesIntroSegmentRows()
    {
        IDbContextFactory<MediaContext> factory = InMemoryFactory(tag: "introseeds");
        Mock<IAudioFingerprinter> fingerprinter = new();
        Mock<IIntroDetector> detector = new();
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: true);
        InMemoryEventBus bus = new();

        AudioFingerprint fp = FakeFingerprint();
        fingerprinter
            .Setup(expression: f =>
                f.FingerprintAsync(
                    It.IsAny<string>(),
                    It.IsAny<FingerprintWindow?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: fp);

        IntroMarker introMarker = new(
            Start: TimeSpan.FromSeconds(seconds: 10),
            End: TimeSpan.FromSeconds(seconds: 95),
            Confidence: 0.91
        );
        detector
            .Setup(expression: d => d.DetectIntro(It.IsAny<IReadOnlyList<AudioFingerprint>>()))
            .Returns(value: introMarker);
        detector
            .Setup(expression: d => d.DetectOutro(It.IsAny<IReadOnlyList<AudioFingerprint>>()))
            .Returns(value: (IntroMarker?)null);

        EncoderOptions options = new() { EnableIntroDetectSubscriber = true };
        using IntroDetectSubscriber subject = new(
            eventBus: bus,
            fingerprinter: fingerprinter.Object,
            introDetector: detector.Object,
            options: options,
            logger: NullLogger<IntroDetectSubscriber>.Instance,
            storage: storage.Object,
            contextFactory: factory
        );

        Ulid libraryId = Ulid.NewUlid();
        await SeedTvSeasonAsync(factory: factory, libraryId: libraryId, tvId: 3, seasonId: 30, episodeIds: [300, 301]);

        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = libraryId,
                LibraryName = "TV",
                ItemsFound = 2,
                Duration = TimeSpan.FromSeconds(seconds: 2),
            }
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        List<ContentSegment> segments = ctx.ContentSegments.ToList();

        segments.Should().HaveCount(expected: 2, because: "one intro segment per episode");
        segments
            .Should()
            .AllSatisfy(expected: s =>
            {
                s.SegmentType.Should().Be(expected: ContentSegmentType.Intro);
                s.StartSeconds.Should().BeApproximately(expectedValue: 10.0, precision: 0.001);
                s.EndSeconds.Should().BeApproximately(expectedValue: 95.0, precision: 0.001);
                s.Confidence.Should().BeApproximately(expectedValue: 0.91, precision: 0.001);
                s.Source.Should().Be(expected: "detector");
                s.EpisodeId.Should().NotBeNull();
            });
    }

    [Fact]
    public async Task OnLibraryScanCompleted_TwoEpisodes_WithDetectedOutro_WritesOutroSegmentRows()
    {
        IDbContextFactory<MediaContext> factory = InMemoryFactory(tag: "outroseeds");
        Mock<IAudioFingerprinter> fingerprinter = new();
        Mock<IIntroDetector> detector = new();
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: true);
        InMemoryEventBus bus = new();

        AudioFingerprint fp = FakeFingerprint();
        fingerprinter
            .Setup(expression: f =>
                f.FingerprintAsync(
                    It.IsAny<string>(),
                    It.IsAny<FingerprintWindow?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: fp);

        detector
            .Setup(expression: d => d.DetectIntro(It.IsAny<IReadOnlyList<AudioFingerprint>>()))
            .Returns(value: (IntroMarker?)null);

        IntroMarker outroMarker = new(
            Start: TimeSpan.FromSeconds(seconds: 1200),
            End: TimeSpan.FromSeconds(seconds: 1380),
            Confidence: 0.85
        );
        detector
            .Setup(expression: d => d.DetectOutro(It.IsAny<IReadOnlyList<AudioFingerprint>>()))
            .Returns(value: outroMarker);

        EncoderOptions options = new() { EnableIntroDetectSubscriber = true };
        using IntroDetectSubscriber subject = new(
            eventBus: bus,
            fingerprinter: fingerprinter.Object,
            introDetector: detector.Object,
            options: options,
            logger: NullLogger<IntroDetectSubscriber>.Instance,
            storage: storage.Object,
            contextFactory: factory
        );

        Ulid libraryId = Ulid.NewUlid();
        await SeedTvSeasonAsync(factory: factory, libraryId: libraryId, tvId: 4, seasonId: 40, episodeIds: [400, 401]);

        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = libraryId,
                LibraryName = "TV",
                ItemsFound = 2,
                Duration = TimeSpan.FromSeconds(seconds: 2),
            }
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        List<ContentSegment> segments = ctx.ContentSegments.ToList();

        segments.Should().HaveCount(expected: 2, because: "one outro segment per episode");
        segments
            .Should()
            .AllSatisfy(expected: s =>
            {
                s.SegmentType.Should().Be(expected: ContentSegmentType.Outro);
                s.StartSeconds.Should().BeApproximately(expectedValue: 1200.0, precision: 0.001);
                s.EndSeconds.Should().BeApproximately(expectedValue: 1380.0, precision: 0.001);
                s.Source.Should().Be(expected: "detector");
            });
    }

    [Fact]
    public async Task OnLibraryScanCompleted_EpisodeWithManualIntro_IsSkipped_OtherEpisodeGetsDetectorSegment()
    {
        IDbContextFactory<MediaContext> factory = InMemoryFactory(tag: "manualskip");
        Mock<IAudioFingerprinter> fingerprinter = new();
        Mock<IIntroDetector> detector = new();
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: true);
        InMemoryEventBus bus = new();

        AudioFingerprint fp = FakeFingerprint();
        fingerprinter
            .Setup(expression: f =>
                f.FingerprintAsync(
                    It.IsAny<string>(),
                    It.IsAny<FingerprintWindow?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: fp);

        IntroMarker introMarker = new(
            Start: TimeSpan.FromSeconds(seconds: 5),
            End: TimeSpan.FromSeconds(seconds: 90),
            Confidence: 0.95
        );
        detector
            .Setup(expression: d => d.DetectIntro(It.IsAny<IReadOnlyList<AudioFingerprint>>()))
            .Returns(value: introMarker);
        detector
            .Setup(expression: d => d.DetectOutro(It.IsAny<IReadOnlyList<AudioFingerprint>>()))
            .Returns(value: (IntroMarker?)null);

        EncoderOptions options = new() { EnableIntroDetectSubscriber = true };
        using IntroDetectSubscriber subject = new(
            eventBus: bus,
            fingerprinter: fingerprinter.Object,
            introDetector: detector.Object,
            options: options,
            logger: NullLogger<IntroDetectSubscriber>.Instance,
            storage: storage.Object,
            contextFactory: factory
        );

        Ulid libraryId = Ulid.NewUlid();
        int episodeWithManual = 500;
        int episodeWithoutManual = 501;
        await SeedTvSeasonAsync(
            factory: factory,
            libraryId: libraryId,
            tvId: 5,
            seasonId: 50,
            episodeIds: [episodeWithManual, episodeWithoutManual]
        );

        await using (MediaContext seedCtx = await factory.CreateDbContextAsync())
        {
            seedCtx.ContentSegments.Add(
                entity: new()
                {
                    EpisodeId = episodeWithManual,
                    SegmentType = ContentSegmentType.Intro,
                    StartSeconds = 8,
                    EndSeconds = 85,
                    Source = "manual",
                    Confidence = 1.0,
                }
            );
            await seedCtx.SaveChangesAsync();
        }

        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = libraryId,
                LibraryName = "TV",
                ItemsFound = 2,
                Duration = TimeSpan.FromSeconds(seconds: 1),
            }
        );

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        List<ContentSegment> segments = ctx.ContentSegments.ToList();

        ContentSegment detectorSegment = segments
            .Where(predicate: s => s.Source == "detector")
            .Should()
            .ContainSingle(because: "only the episode without manual coverage gets a detector segment")
            .Which;

        detectorSegment.EpisodeId.Should().Be(expected: episodeWithoutManual);
        detectorSegment.SegmentType.Should().Be(expected: ContentSegmentType.Intro);

        segments
            .Where(predicate: s => s.Source == "manual" && s.EpisodeId == episodeWithManual)
            .Should()
            .ContainSingle(because: "manual segment is untouched");
    }

    [Fact]
    public async Task OnLibraryScanCompleted_FingerprintThrows_DoesNotPropagate_WritesNoSegments()
    {
        IDbContextFactory<MediaContext> factory = InMemoryFactory(tag: "fpthrows");
        Mock<IAudioFingerprinter> fingerprinter = new();
        Mock<IIntroDetector> detector = new();
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.Exists(It.IsAny<string>())).Returns(value: true);
        InMemoryEventBus bus = new();

        fingerprinter
            .Setup(expression: f =>
                f.FingerprintAsync(
                    It.IsAny<string>(),
                    It.IsAny<FingerprintWindow?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "chromaprint unavailable"));

        EncoderOptions options = new() { EnableIntroDetectSubscriber = true };
        using IntroDetectSubscriber subject = new(
            eventBus: bus,
            fingerprinter: fingerprinter.Object,
            introDetector: detector.Object,
            options: options,
            logger: NullLogger<IntroDetectSubscriber>.Instance,
            storage: storage.Object,
            contextFactory: factory
        );

        Ulid libraryId = Ulid.NewUlid();
        await SeedTvSeasonAsync(factory: factory, libraryId: libraryId, tvId: 6, seasonId: 60, episodeIds: [600, 601]);

        Func<Task> act = () =>
            bus.PublishAsync(
                @event: new LibraryScanCompletedEvent
                {
                    LibraryId = libraryId,
                    LibraryName = "TV",
                    ItemsFound = 2,
                    Duration = TimeSpan.Zero,
                }
            );

        await act.Should().NotThrowAsync(because: "fingerprint failures are caught-and-logged per episode");

        await using MediaContext ctx = await factory.CreateDbContextAsync();
        ctx.ContentSegments.Should()
            .BeEmpty(because: "fingerprinting failed for all episodes so no segments can be written");
    }

    [Fact]
    public void Constructor_SubscribesToLibraryScanCompletedEvent()
    {
        Mock<IEventBus> bus = new();
        Mock<IDisposable> stubSub = new();
        bus.Setup(expression: b =>
                b.Subscribe<LibraryScanCompletedEvent>(
                    It.IsAny<Func<LibraryScanCompletedEvent, CancellationToken, Task>>()
                )
            )
            .Returns(value: stubSub.Object);
        Mock<IStorage> storage = new();
        EncoderOptions options = new() { EnableIntroDetectSubscriber = true };

        using IntroDetectSubscriber _ = new(
            eventBus: bus.Object,
            fingerprinter: Mock.Of<IAudioFingerprinter>(),
            introDetector: Mock.Of<IIntroDetector>(),
            options: options,
            logger: NullLogger<IntroDetectSubscriber>.Instance,
            storage: storage.Object,
            contextFactory: InMemoryFactory(tag: "ctor")
        );

        bus.Verify(
            expression: b =>
                b.Subscribe<LibraryScanCompletedEvent>(
                    It.IsAny<Func<LibraryScanCompletedEvent, CancellationToken, Task>>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public void Dispose_ReleasesSubscription()
    {
        Mock<IDisposable> subscription = new();
        Mock<IEventBus> bus = new();
        bus.Setup(expression: b =>
                b.Subscribe<LibraryScanCompletedEvent>(
                    It.IsAny<Func<LibraryScanCompletedEvent, CancellationToken, Task>>()
                )
            )
            .Returns(value: subscription.Object);

        IntroDetectSubscriber subject = new(
            eventBus: bus.Object,
            fingerprinter: Mock.Of<IAudioFingerprinter>(),
            introDetector: Mock.Of<IIntroDetector>(),
            options: new(),
            logger: NullLogger<IntroDetectSubscriber>.Instance,
            storage: Mock.Of<IStorage>(),
            contextFactory: InMemoryFactory(tag: "dispose")
        );

        subject.Dispose();

        subscription.Verify(expression: s => s.Dispose(), times: Times.Once);
    }
}
