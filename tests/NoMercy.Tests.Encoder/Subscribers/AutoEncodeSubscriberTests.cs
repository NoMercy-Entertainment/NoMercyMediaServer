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
/// AutoEncodeSubscriber dispatches an encode when a freshly-scanned media
/// file lives under a watched folder. The contract:
///   - opt-out (EnableAutoEncodeSubscriber=false) → no DB call, no orchestrator.
///   - no watched folders → no DB call, no orchestrator.
///   - VideoFile missing for media → log+return, no orchestrator.
///   - no path prefix matches → no orchestrator.
///   - prefix match → orchestrator called with the right profile.
///   - orchestrator throws → swallowed (we don't want event-bus blowups).
/// </summary>
public class AutoEncodeSubscriberTests
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
            .UseInMemoryDatabase($"autoenc-{suffix}-{Ulid.NewUlid()}")
            .Options;
        Mock<IDbContextFactory<MediaContext>> mock = new();
        mock.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new(options));
        mock.Setup(f => f.CreateDbContext()).Returns(() => new(options));
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

    [Fact]
    public async Task OnMediaFilesScanned_DisabledViaOptions_DoesNotTouchOrchestrator()
    {
        // Hard kill-switch: subscriber is constructed (so it logs once) but
        // does nothing when the event fires.
        Mock<IEventBus> bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = false };
        IDbContextFactory<MediaContext> factory = InMemoryFactory("disabled");

        AutoEncodeSubscriber subject = new(
            bus.Object,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        await subject.OnMediaFilesScanned(
            new() { MediaId = 42, LibraryId = Ulid.NewUlid() },
            CancellationToken.None
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
    public async Task OnMediaFilesScanned_NoWatchedFolders_DoesNotQueryDb()
    {
        // Empty WatchedFolderProfiles → cheap exit before opening a DB context.
        Mock<IEventBus> bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        // intentionally untouched: WatchedFolderProfiles is empty.
        Mock<IDbContextFactory<MediaContext>> factory = new();

        AutoEncodeSubscriber subject = new(
            bus.Object,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory.Object
        );

        await subject.OnMediaFilesScanned(
            new() { MediaId = 42, LibraryId = Ulid.NewUlid() },
            CancellationToken.None
        );

        factory.Verify(
            f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "no point opening a DB context when there are no folders to match against"
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
    public async Task OnMediaFilesScanned_VideoFileMissing_DoesNotDispatch()
    {
        // Scanner published the event but no VideoFile row exists for the
        // media id (race / scanner bug). Must log+return, never crash.
        Mock<IEventBus> bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        options.WatchedFolderProfiles["/media/watch/"] = MakeProfile("p");
        IDbContextFactory<MediaContext> factory = InMemoryFactory("missing");

        AutoEncodeSubscriber subject = new(
            bus.Object,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        await subject.OnMediaFilesScanned(
            new() { MediaId = 999, LibraryId = Ulid.NewUlid() },
            CancellationToken.None
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
    public async Task OnMediaFilesScanned_PathPrefixMatches_DispatchesEncodeWithProfile()
    {
        Mock<IEventBus> bus = new();
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

        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        EncodingProfile profile = MakeProfile("watched-1080p");
        options.WatchedFolderProfiles["/media/watch/"] = profile;
        IDbContextFactory<MediaContext> factory = InMemoryFactory("match");
        await SeedVideoFileAsync(
            factory,
            mediaId: 7,
            hostFolder: "/media/watch/",
            filename: "movie.mkv"
        );

        AutoEncodeSubscriber subject = new(
            bus.Object,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        await subject.OnMediaFilesScanned(
            new() { MediaId = 7, LibraryId = Ulid.NewUlid() },
            CancellationToken.None
        );

        orch.Verify(
            o =>
                o.EncodeAsync(
                    It.Is<EncodingRequest>(req =>
                        req.Profile == profile && req.InputPath == "/media/watch/movie.mkv"
                    ),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task OnMediaFilesScanned_PathPrefixDoesNotMatch_DoesNotDispatch()
    {
        // VideoFile exists but its host folder is unrelated to the configured
        // watched folder — must skip silently.
        Mock<IEventBus> bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        options.WatchedFolderProfiles["/media/auto-encode/"] = MakeProfile("p");
        IDbContextFactory<MediaContext> factory = InMemoryFactory("nomatch");
        await SeedVideoFileAsync(
            factory,
            mediaId: 8,
            hostFolder: "/media/manual/",
            filename: "manual.mkv"
        );

        AutoEncodeSubscriber subject = new(
            bus.Object,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        await subject.OnMediaFilesScanned(
            new() { MediaId = 8, LibraryId = Ulid.NewUlid() },
            CancellationToken.None
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
    public async Task OnMediaFilesScanned_OrchestratorThrows_DoesNotPropagate()
    {
        // The event bus shouldn't blow up because one subscriber threw —
        // catch + log + return so the rest of the pipeline keeps running.
        Mock<IEventBus> bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        orch.Setup(o =>
                o.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("encoder is down"));

        EncoderOptions options = new() { EnableAutoEncodeSubscriber = true };
        options.WatchedFolderProfiles["/media/watch/"] = MakeProfile("p");
        IDbContextFactory<MediaContext> factory = InMemoryFactory("orch-throws");
        await SeedVideoFileAsync(
            factory,
            mediaId: 9,
            hostFolder: "/media/watch/",
            filename: "movie.mkv"
        );

        AutoEncodeSubscriber subject = new(
            bus.Object,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        Func<Task> act = () =>
            subject.OnMediaFilesScanned(
                new() { MediaId = 9, LibraryId = Ulid.NewUlid() },
                CancellationToken.None
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_SubscribesToEventBus()
    {
        // Ctor MUST register one subscription so PublishAsync delivers
        // MediaFilesScannedEvent to OnMediaFilesScanned.
        Mock<IEventBus> bus = new();
        Mock<IEncodingOrchestrator> orch = new();
        EncoderOptions options = new();
        IDbContextFactory<MediaContext> factory = InMemoryFactory("ctor-subscribe");

        _ = new AutoEncodeSubscriber(
            bus.Object,
            orch.Object,
            options,
            NullLogger<AutoEncodeSubscriber>.Instance,
            factory
        );

        bus.Verify(
            b =>
                b.Subscribe<MediaFilesScannedEvent>(
                    It.IsAny<Func<MediaFilesScannedEvent, CancellationToken, Task>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Dispose_ReleasesSubscription()
    {
        // After Dispose the subscription IDisposable must be disposed.
        Mock<IDisposable> subscription = new();
        Mock<IEventBus> bus = new();
        bus.Setup(b =>
                b.Subscribe<MediaFilesScannedEvent>(
                    It.IsAny<Func<MediaFilesScannedEvent, CancellationToken, Task>>()
                )
            )
            .Returns(subscription.Object);

        AutoEncodeSubscriber subject = new(
            bus.Object,
            Mock.Of<IEncodingOrchestrator>(),
            new(),
            NullLogger<AutoEncodeSubscriber>.Instance,
            InMemoryFactory("dispose")
        );

        subject.Dispose();

        subscription.Verify(s => s.Dispose(), Times.Once);
    }
}
