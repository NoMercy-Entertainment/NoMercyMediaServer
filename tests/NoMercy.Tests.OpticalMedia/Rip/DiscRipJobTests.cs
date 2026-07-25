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
[Collection("EventBusProvider")]
[Trait("Category", "Unit")]
public class DiscRipJobTests
{
    private static RipRequest MakeRequest(string drivePath = "D:\\") =>
        new(
            drivePath,
            [1],
            null,
            new(
                "Test Movie",
                2024,
                MediaType.Movie,
                null
            ),
            Ulid.NewUlid(),
            Ulid.NewUlid(),
            null,
            [],
            [],
            RipMode.RipToRaw
        );

    private static DiscIdentificationService MakeIdentificationService() =>
        new([], NullLogger<DiscIdentificationService>.Instance);

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
            request,
            Path.GetTempPath(),
            null,
            null,
            null
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
            .Setup(r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new DiscDriveBusyException("D:\\"));

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>((evt, _) => published.Add(evt))
            .Returns(Task.CompletedTask);

        EventBusProvider.Configure(busMock.Object);

        DiscRipJob job = MakeJob(MakeRequest(), ripperMock.Object);
        await job.Handle();

        DriveStateChangedEvent? errorEvent = published.FirstOrDefault(e =>
            e.DriveStateData.Method == "rip_error"
        );

        errorEvent.Should().NotBeNull("a rip_error event must be published when the drive is busy");
        errorEvent!.DriveStateData.Drive.Should().Be("D:\\");
        errorEvent.DriveStateData.Message.Should().Contain("already in use");
    }

    [Fact]
    public async Task Handle_WhenDriveBusy_DoesNotPublishRipComplete()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new DiscDriveBusyException("D:\\"));

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>((evt, _) => published.Add(evt))
            .Returns(Task.CompletedTask);

        EventBusProvider.Configure(busMock.Object);

        DiscRipJob job = MakeJob(MakeRequest(), ripperMock.Object);
        await job.Handle();

        published.Should().NotContain(e => e.DriveStateData.Method == "rip_complete");
    }

    // ── General rip failure ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRipperThrowsGenericException_PublishesRipError()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("FFmpeg crashed"));

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>((evt, _) => published.Add(evt))
            .Returns(Task.CompletedTask);

        EventBusProvider.Configure(busMock.Object);

        DiscRipJob job = MakeJob(MakeRequest(), ripperMock.Object);
        await job.Handle();

        DriveStateChangedEvent? errorEvent = published.FirstOrDefault(e =>
            e.DriveStateData.Method == "rip_error"
        );

        errorEvent.Should().NotBeNull();
        errorEvent!.DriveStateData.Message.Should().Contain("FFmpeg crashed");
    }

    // ── Happy path (RipToRaw — no move, no DB needed) ─────────────────────

    [Fact]
    public async Task Handle_HappyPath_PublishesStartedThenComplete()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new(
                    1,
                    Path.Combine(Path.GetTempPath(), "title_01.mkv"),
                    true,
                    TimeSpan.FromMinutes(90),
                    1_000_000,
                    null
                ),
            ]);

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>((evt, _) => published.Add(evt))
            .Returns(Task.CompletedTask);

        EventBusProvider.Configure(busMock.Object);

        DiscRipJob job = MakeJob(MakeRequest(), ripperMock.Object);
        await job.Handle();

        string[] methods = published.Select(e => e.DriveStateData.Method).ToArray();

        methods.Should().Contain("rip_started");
        methods.Should().Contain("rip_complete");
        methods.Should().NotContain("rip_error");
        methods[0].Should().Be("rip_started", "started must be the first event");
    }

    [Fact]
    public async Task Handle_HappyPath_JobIdConsistentAcrossEvents()
    {
        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new(
                    1,
                    Path.Combine(Path.GetTempPath(), "t.mkv"),
                    true,
                    TimeSpan.Zero,
                    0,
                    null
                ),
            ]);

        List<DriveStateChangedEvent> published = [];
        Mock<IEventBus> busMock = new();
        busMock
            .Setup(b =>
                b.PublishAsync(It.IsAny<DriveStateChangedEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<DriveStateChangedEvent, CancellationToken>((evt, _) => published.Add(evt))
            .Returns(Task.CompletedTask);

        EventBusProvider.Configure(busMock.Object);

        DiscRipJob job = MakeJob(MakeRequest(), ripperMock.Object);
        await job.Handle();

        string? expectedJobId = job.JobId;
        published.Should().AllSatisfy(e => e.DriveStateData.JobId.Should().Be(expectedJobId));
    }

    // ── EventBusProvider not configured ───────────────────────────────────

    [Fact]
    public async Task Handle_WhenEventBusNotConfigured_DoesNotThrow()
    {
        typeof(EventBusProvider)
            .GetField(
                "_instance",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            )!
            .SetValue(null, null);

        Mock<IDiscRipper> ripperMock = new();
        ripperMock
            .Setup(r =>
                r.RipAsync(
                    It.IsAny<RipRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        DiscRipJob job = MakeJob(MakeRequest(), ripperMock.Object);

        Exception? ex = await Record.ExceptionAsync(() => job.Handle());
        ex.Should().BeNull();
    }
}
