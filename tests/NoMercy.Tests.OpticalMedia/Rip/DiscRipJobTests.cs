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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Events;
using NoMercy.Events.DriveMonitor;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage;

namespace NoMercy.Tests.OpticalMedia.Rip;

/// <summary>
/// Unit tests for <see cref="DiscRipJob"/>. The DB interactions are skipped
/// by setting <see cref="RipMode.RipToRaw"/> (bypasses the move-and-import
/// branch that needs a live <c>MediaContext</c>). EventBusProvider is
/// configured with a mock bus for each test that needs event verification.
/// </summary>
[Collection(name: "EventBusProvider")]
[Trait(name: "Category", value: "Unit")]
public class DiscRipJobTests
{
    private static RipRequest MakeRequest(string drivePath = "D:\\") =>
        new(
            DrivePath: drivePath,
            SelectedTitleIndices: [1],
            MetadataId: null,
            Custom: new(
                Title: "Test Movie",
                Year: 2024,
                Type: MediaType.Movie,
                PosterUrl: null
            ),
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: [],
            Mode: RipMode.RipToRaw
        );

    private static DiscIdentificationService MakeIdentificationService() =>
        new(identifiers: [], logger: NullLogger<DiscIdentificationService>.Instance);

    private static DiscRipJob MakeJob(
        RipRequest request,
        IDiscRipper ripper,
        DiscIdentificationService? identificationService = null,
        IStorageFactory? storageFactory = null,
        IStorageDriver? storageDriver = null,
        DriveLockRegistry? lockRegistry = null
    )
    {
        DiscRipJob job = new(
            request: request,
            outputDir: Path.GetTempPath(),
            targetFolderId: null,
            targetLibraryId: null,
            targetLibraryType: null
        );

        job.DiscRipper = ripper;
        job.IdentificationService = identificationService ?? MakeIdentificationService();
        job.StorageFactory = storageFactory ?? Mock.Of<IStorageFactory>();
        job.StorageDriver = storageDriver ?? Mock.Of<IStorageDriver>();
        job.DriveLockRegistry = lockRegistry ?? new DriveLockRegistry();
        job.LoggerFactory = NullLoggerFactory.Instance;

        return job;
    }

    // ── Drive-lock rejection ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenDriveBusy_PublishesRipErrorEvent()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new DiscDriveBusyException(driveKey: "D:\\"));

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);

        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(request: MakeRequest(), ripper: ripperMock.Object);
        await job.Handle();

        DriveStateChangedEvent? errorEvent = published.FirstOrDefault(predicate: e =>
            e.DriveStateData.Method == "rip_error"
        );

        errorEvent.Should().NotBeNull(because: "a rip_error event must be published when the drive is busy");
        errorEvent!.DriveStateData.Drive.Should().Be(expected: "D:\\");
        errorEvent.DriveStateData.Message.Should().Contain(expected: "already in use");
    }

    [Fact]
    public async Task Handle_WhenDriveBusy_DoesNotPublishRipComplete()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new DiscDriveBusyException(driveKey: "D:\\"));

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);

        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(request: MakeRequest(), ripper: ripperMock.Object);
        await job.Handle();

        published.Should().NotContain(predicate: e => e.DriveStateData.Method == "rip_complete");
    }

    // ── General rip failure ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRipperThrowsGenericException_PublishesRipError()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "FFmpeg crashed"));

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);

        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(request: MakeRequest(), ripper: ripperMock.Object);
        await job.Handle();

        DriveStateChangedEvent? errorEvent = published.FirstOrDefault(predicate: e =>
            e.DriveStateData.Method == "rip_error"
        );

        errorEvent.Should().NotBeNull();
        errorEvent!.DriveStateData.Message.Should().Contain(expected: "FFmpeg crashed");
    }

    // ── Happy path (RipToRaw — no move, no DB needed) ─────────────────────

    [Fact]
    public async Task Handle_HappyPath_PublishesStartedThenComplete()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value:
            [
                new(
                    TitleIndex: 1,
                    OutputPath: Path.Combine(path1: Path.GetTempPath(), path2: "title_01.mkv"),
                    Success: true,
                    Duration: TimeSpan.FromMinutes(minutes: 90),
                    OutputSizeBytes: 1_000_000,
                    Error: null
                ),
            ]);

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);

        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(request: MakeRequest(), ripper: ripperMock.Object);
        await job.Handle();

        string[] methods = published.Select(selector: e => e.DriveStateData.Method).ToArray();

        methods.Should().Contain(expected: "rip_started");
        methods.Should().Contain(expected: "rip_complete");
        methods.Should().NotContain(unexpected: "rip_error");
        methods[0].Should().Be(expected: "rip_started", because: "started must be the first event");
    }

    [Fact]
    public async Task Handle_HappyPath_JobIdConsistentAcrossEvents()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value:
            [
                new(
                    TitleIndex: 1,
                    OutputPath: Path.Combine(path1: Path.GetTempPath(), path2: "t.mkv"),
                    Success: true,
                    Duration: TimeSpan.Zero,
                    OutputSizeBytes: 0,
                    Error: null
                ),
            ]);

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(expression: b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>(action: (evt, _) => published.Add(item: evt))
            .Returns(value: Task.CompletedTask);

        EventBusProvider.Configure(eventBus: busMock.Object);

        DiscRipJob job = MakeJob(request: MakeRequest(), ripper: ripperMock.Object);
        await job.Handle();

        string? expectedJobId = job.JobId;
        published.Should().AllSatisfy(expected: e => e.DriveStateData.JobId.Should().Be(expected: expectedJobId));
    }

    // ── EventBusProvider not configured ───────────────────────────────────

    [Fact]
    public async Task Handle_WhenEventBusNotConfigured_DoesNotThrow()
    {
        typeof(EventBusProvider)
            .GetField(
                name: "_instance",
                bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            )!
            .SetValue(obj: null, value: null);

        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(expression: r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: []);

        DiscRipJob job = MakeJob(request: MakeRequest(), ripper: ripperMock.Object);

        Exception? ex = await Record.ExceptionAsync(testCode: () => job.Handle());
        ex.Should().BeNull();
    }
}
