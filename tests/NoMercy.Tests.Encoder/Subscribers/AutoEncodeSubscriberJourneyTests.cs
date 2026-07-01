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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subscribers;
using NoMercy.Events;
using NoMercy.Events.Library;

namespace NoMercy.Tests.Encoder.Subscribers;

/// <summary>
/// Journey tests: publish the real <see cref="MediaFilesScannedEvent"/> through a
/// real <see cref="InMemoryEventBus"/> and assert the full reaction chain.
/// The handler auto-subscribes in its constructor; we never call
/// <c>OnMediaFilesScanned</c> directly — the bus routes it.
///
/// These tests prove the CONNECTION chain, not just single-hop method calls:
///   publish → subscription dispatch → guard evaluation → orchestrator decision
///
/// Guard conditions (disabled / no folders / missing VideoFile / path mismatch)
/// are proven by asserting the orchestrator was NOT called after a real publish.
/// </summary>
[Trait("Category", "Journey")]
public class AutoEncodeSubscriberJourneyTests
{
    private static EncodingProfile MakeProfile(string name) =>
        new(
            Id: Ulid.NewUlid(),
            Name: name,
            Container: Container.HlsTs,
            Video: null,
            Audio: [],
            Subtitles: []
        );

    private static IDbContextFactory<MediaContext> InMemoryFactory(string suffix)
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase($"aes-journey-{suffix}-{Ulid.NewUlid()}")
            .Options;
        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MediaContext(options));
        mock.Setup(f => f.CreateDbContext()).Returns(() => new MediaContext(options));
        return mock.Object;
    }

    private static async Task SeedVideoFileAsync(
        IDbContextFactory<MediaContext> factory,
        int mediaId,
        string hostFolder,
        string filename,
        bool isEpisode = false
    )
    {
        await using MediaContext ctx = await factory.CreateDbContextAsync();
        VideoFile vf = new()
        {
            Filename = filename,
            HostFolder = hostFolder,
            Folder = hostFolder,
            EpisodeId = isEpisode ? mediaId : null,
            MovieId = isEpisode ? null : mediaId,
        };
        ctx.VideoFiles.Add(vf);
        await ctx.SaveChangesAsync();
    }

    private static Mock<IEncodingOrchestrator> SucceedingOrchestrator()
    {
        Mock<IEncodingOrchestrator> orch = new();
        orch.Setup(o =>
                o.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(true, "/out", TimeSpan.Zero, null, new(0, 0, 0, "x", null))
            );
        return orch;
    }

    [Fact]
    public async Task Journey_PathMatch_BusPublish_ReachesOrchestrator()
    {
        InMemoryEventBus bus = new();
        Mock<IEncodingOrchestrator> orch = SucceedingOrchestrator();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        EncodingProfile profile = MakeProfile("hls-1080p");
        options.WatchedFolderProfiles["/media/watch/"] = profile;
        IDbContextFactory<MediaContext> factory = InMemoryFactory("journey-match");
        await SeedVideoFileAsync(
            factory,
            mediaId: 10,
            hostFolder: "/media/watch/",
            filename: "film.mkv"
        );

        using AutoEncodeSubscriber subscriber = new(
            bus,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = 10, LibraryId = Ulid.NewUlid() }
        );

        orch.Verify(
            o =>
                o.EncodeAsync(
                    It.Is<EncodingRequest>(req =>
                        req.Profile == profile && req.InputPath == "/media/watch/film.mkv"
                    ),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "publishing through the real bus must route to the orchestrator with the matched profile"
        );
    }

    [Fact]
    public async Task Journey_Disabled_BusPublish_NeverReachesOrchestrator()
    {
        InMemoryEventBus bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = false };
        options.WatchedFolderProfiles["/media/watch/"] = MakeProfile("p");
        IDbContextFactory<MediaContext> factory = InMemoryFactory("journey-disabled");
        await SeedVideoFileAsync(
            factory,
            mediaId: 20,
            hostFolder: "/media/watch/",
            filename: "f.mkv"
        );

        using AutoEncodeSubscriber subscriber = new(
            bus,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = 20, LibraryId = Ulid.NewUlid() }
        );

        orch.Verify(
            o =>
                o.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "kill-switch must stop the chain before the orchestrator is ever reached"
        );
    }

    [Fact]
    public async Task Journey_NoWatchedFolders_BusPublish_NeverOpensDb()
    {
        InMemoryEventBus bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        Mock<IDbContextFactory<MediaContext>> factory = new();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };

        using AutoEncodeSubscriber subscriber = new(
            bus,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory.Object
        );

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = 30, LibraryId = Ulid.NewUlid() }
        );

        factory.Verify(
            f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "when no watched folders exist, the chain must exit before opening a DB context"
        );
        orch.Verify(
            o =>
                o.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Journey_MissingVideoFile_BusPublish_NeverReachesOrchestrator()
    {
        InMemoryEventBus bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        options.WatchedFolderProfiles["/media/watch/"] = MakeProfile("p");
        IDbContextFactory<MediaContext> factory = InMemoryFactory("journey-missing-vf");

        using AutoEncodeSubscriber subscriber = new(
            bus,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = 999, LibraryId = Ulid.NewUlid() }
        );

        orch.Verify(
            o =>
                o.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "missing VideoFile row must break the chain before the orchestrator is called"
        );
    }

    [Fact]
    public async Task Journey_PathPrefixMismatch_BusPublish_NeverReachesOrchestrator()
    {
        InMemoryEventBus bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        options.WatchedFolderProfiles["/media/auto-encode/"] = MakeProfile("p");
        IDbContextFactory<MediaContext> factory = InMemoryFactory("journey-mismatch");
        await SeedVideoFileAsync(
            factory,
            mediaId: 40,
            hostFolder: "/media/unrelated/",
            filename: "f.mkv"
        );

        using AutoEncodeSubscriber subscriber = new(
            bus,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = 40, LibraryId = Ulid.NewUlid() }
        );

        orch.Verify(
            o =>
                o.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "a host folder that doesn't start with any watched-folder key must not dispatch an encode"
        );
    }

    [Fact]
    public async Task Journey_OrchestratorThrows_BusDoesNotPropagate()
    {
        InMemoryEventBus bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        orch.Setup(o =>
                o.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("encoder offline"));

        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        options.WatchedFolderProfiles["/media/watch/"] = MakeProfile("p");
        IDbContextFactory<MediaContext> factory = InMemoryFactory("journey-orch-throws");
        await SeedVideoFileAsync(
            factory,
            mediaId: 50,
            hostFolder: "/media/watch/",
            filename: "f.mkv"
        );

        using AutoEncodeSubscriber subscriber = new(
            bus,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        Func<Task> act = () =>
            bus.PublishAsync(
                new MediaFilesScannedEvent { MediaId = 50, LibraryId = Ulid.NewUlid() }
            );

        await act.Should()
            .NotThrowAsync(
                "an orchestrator failure must be swallowed — the event bus must not blow up"
            );
    }

    [Fact]
    public async Task Journey_EpisodeFile_PathMatch_ReachesOrchestrator()
    {
        InMemoryEventBus bus = new();
        Mock<IEncodingOrchestrator> orch = SucceedingOrchestrator();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        EncodingProfile profile = MakeProfile("tv-1080p");
        options.WatchedFolderProfiles["/media/tv/"] = profile;
        IDbContextFactory<MediaContext> factory = InMemoryFactory("journey-episode");
        await SeedVideoFileAsync(
            factory,
            mediaId: 60,
            hostFolder: "/media/tv/",
            filename: "s01e01.mkv",
            isEpisode: true
        );

        using AutoEncodeSubscriber subscriber = new(
            bus,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = 60, LibraryId = Ulid.NewUlid() }
        );

        orch.Verify(
            o =>
                o.EncodeAsync(
                    It.Is<EncodingRequest>(req =>
                        req.Profile == profile && req.InputPath == "/media/tv/s01e01.mkv"
                    ),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "episode VideoFiles must route through the same chain as movie VideoFiles"
        );
    }

    [Fact]
    public async Task Journey_Dispose_StopsChain_EventAfterDisposeNotDelivered()
    {
        InMemoryEventBus bus = new();
        Mock<IEncodingOrchestrator> orch = SucceedingOrchestrator();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        options.WatchedFolderProfiles["/media/watch/"] = MakeProfile("p");
        IDbContextFactory<MediaContext> factory = InMemoryFactory("journey-dispose");
        await SeedVideoFileAsync(
            factory,
            mediaId: 70,
            hostFolder: "/media/watch/",
            filename: "f.mkv"
        );

        AutoEncodeSubscriber subscriber = new(
            bus,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        subscriber.Dispose();

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = 70, LibraryId = Ulid.NewUlid() }
        );

        orch.Verify(
            o =>
                o.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "after Dispose the subscription is unregistered — the chain must be severed"
        );
    }

    [Fact]
    public async Task Journey_MultipleSubscribers_IndependentChains_BothReachOrchestrator()
    {
        InMemoryEventBus bus = new();
        Mock<IEncodingOrchestrator> orch1 = SucceedingOrchestrator();
        Mock<IEncodingOrchestrator> orch2 = SucceedingOrchestrator();

        EncoderOptions options1 = new() { EnableAutoEncodeSubscriber = true };
        EncoderOptions options2 = new() { EnableAutoEncodeSubscriber = true };
        EncodingProfile profile1 = MakeProfile("chain-1");
        EncodingProfile profile2 = MakeProfile("chain-2");
        options1.WatchedFolderProfiles["/media/a/"] = profile1;
        options2.WatchedFolderProfiles["/media/b/"] = profile2;

        IDbContextFactory<MediaContext> factory1 = InMemoryFactory("journey-multi-1");
        IDbContextFactory<MediaContext> factory2 = InMemoryFactory("journey-multi-2");
        await SeedVideoFileAsync(factory1, mediaId: 80, hostFolder: "/media/a/", filename: "f.mkv");
        await SeedVideoFileAsync(factory2, mediaId: 80, hostFolder: "/media/b/", filename: "f.mkv");

        using AutoEncodeSubscriber sub1 = new(
            bus,
            orch1.Object,
            options1,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory1
        );
        using AutoEncodeSubscriber sub2 = new(
            bus,
            orch2.Object,
            options2,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory2
        );

        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = 80, LibraryId = Ulid.NewUlid() }
        );

        orch1.Verify(
            o =>
                o.EncodeAsync(
                    It.Is<EncodingRequest>(req => req.Profile == profile1),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "first subscriber's chain must fire independently"
        );
        orch2.Verify(
            o =>
                o.EncodeAsync(
                    It.Is<EncodingRequest>(req => req.Profile == profile2),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "second subscriber's chain must fire independently"
        );
    }
}
