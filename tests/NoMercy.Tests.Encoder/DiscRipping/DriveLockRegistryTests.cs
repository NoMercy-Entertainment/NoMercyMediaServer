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

using Microsoft.Extensions.Logging;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.DiscRipping;

public class DriveLockRegistryTests
{
    // -----------------------------------------------------------------------
    // DriveLockRegistry unit tests
    // -----------------------------------------------------------------------

    [Fact]
    public void TryAcquire_FirstCaller_ReturnsTrue()
    {
        DriveLockRegistry registry = new();

        bool acquired = registry.TryAcquire(driveKey: "drive-uuid-1", driveLock: out DriveLock? lock1);

        acquired.Should().BeTrue();
        lock1.Should().NotBeNull();
        lock1!.Dispose();
    }

    [Fact]
    public void TryAcquire_SecondCallerSameDrive_ReturnsFalse()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire(driveKey: "drive-uuid-1", driveLock: out DriveLock? lock1);

        bool acquired = registry.TryAcquire(driveKey: "drive-uuid-1", driveLock: out DriveLock? lock2);

        acquired.Should().BeFalse();
        lock2.Should().BeNull();
        lock1!.Dispose();
    }

    [Fact]
    public void TryAcquire_TwoCallersDifferentDrives_BothSucceed()
    {
        DriveLockRegistry registry = new();

        bool acquired1 = registry.TryAcquire(driveKey: "drive-uuid-A", driveLock: out DriveLock? lock1);
        bool acquired2 = registry.TryAcquire(driveKey: "drive-uuid-B", driveLock: out DriveLock? lock2);

        acquired1.Should().BeTrue();
        acquired2.Should().BeTrue();
        lock1!.Dispose();
        lock2!.Dispose();
    }

    [Fact]
    public void TryAcquire_AfterRelease_Succeeds()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire(driveKey: "drive-uuid-1", driveLock: out DriveLock? lock1);
        lock1!.Dispose();

        bool acquired = registry.TryAcquire(driveKey: "drive-uuid-1", driveLock: out DriveLock? lock2);

        acquired.Should().BeTrue();
        lock2!.Dispose();
    }

    [Fact]
    public void DriveLock_DisposeIsIdempotent()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire(driveKey: "drive-uuid-1", driveLock: out DriveLock? lock1);

        lock1!.Dispose();
        Action second = () => lock1.Dispose();
        second.Should().NotThrow();

        // After double-dispose, the drive must be acquirable again
        bool acquired = registry.TryAcquire(driveKey: "drive-uuid-1", driveLock: out DriveLock? lock2);
        acquired.Should().BeTrue();
        lock2!.Dispose();
    }

    // -----------------------------------------------------------------------
    // DiscRipper integration: lock behaviour via RipAsync
    // -----------------------------------------------------------------------

    private static ProcessResult SuccessResult() =>
        new(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero);

    private static ProcessResult FailureResult() =>
        new(ExitCode: 1, StdOut: "", StdErr: "err", Duration: TimeSpan.Zero);

    private static IDiscRipper BuildRipper(DriveLockRegistry registry, IProcessRunner processRunner)
    {
        EncoderOptions opts = new()
        {
            FfmpegPathOverride = "ffmpeg",
            FfprobePathOverride = "ffprobe",
        };

        Mock<IStorage> storageMock = new();
        storageMock.Setup(expression: s => s.CreateDirectory(It.IsAny<string>()));
        storageMock
            .Setup(expression: s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns(valueFunction: (string p) => new(path: p));
        storageMock.Setup(expression: s => s.SizeOrZero(It.IsAny<string>())).Returns(value: 1024L);

        Mock<ILogger<DiscRipper>> loggerMock = new();

        return new DiscRipper(options: opts, processRunner: processRunner, storage: storageMock.Object, driveLockRegistry: registry, logger: loggerMock.Object);
    }

    private static RipRequest MakeRequest(string drivePath, string? volumeUuid = null) =>
        new(
            DrivePath: drivePath,
            SelectedTitleIndices: [1],
            MetadataId: null,
            Custom: null,
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: [],
            VolumeUuid: volumeUuid
        );

    [Fact]
    public async Task RipAsync_SameDriveConcurrently_SecondThrowsDiscDriveBusyException()
    {
        DriveLockRegistry registry = new();

        TaskCompletionSource<ProcessResult> tcs = new();
        Mock<IProcessRunner> runner1 = new();
        runner1
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(valueFunction: () => tcs.Task);

        Mock<IProcessRunner> runner2 = new();

        IDiscRipper ripper1 = BuildRipper(registry: registry, processRunner: runner1.Object);
        IDiscRipper ripper2 = BuildRipper(registry: registry, processRunner: runner2.Object);

        RipRequest request = MakeRequest(drivePath: "/dev/sr0", volumeUuid: "vol-uuid-same");

        Task<DiscRipResult[]> rip1 = ripper1.RipAsync(request: request, outputDirectory: "/tmp/rip1", ct: CancellationToken.None);

        // Give the first rip time to acquire the lock
        await Task.Delay(millisecondsDelay: 50);

        Func<Task> rip2 = () => ripper2.RipAsync(request: request, outputDirectory: "/tmp/rip2", ct: CancellationToken.None);
        await rip2.Should().ThrowAsync<DiscDriveBusyException>();

        // Unblock rip1 and let it finish cleanly
        tcs.SetResult(result: SuccessResult());
        await rip1;
    }

    [Fact]
    public async Task RipAsync_DifferentDrivesConcurrently_BothProceed()
    {
        DriveLockRegistry registry = new();

        TaskCompletionSource<ProcessResult> tcsA = new();
        Mock<IProcessRunner> runnerA = new();
        runnerA
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(valueFunction: () => tcsA.Task);

        Mock<IProcessRunner> runnerB = new();
        runnerB
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: SuccessResult());

        IDiscRipper ripperA = BuildRipper(registry: registry, processRunner: runnerA.Object);
        IDiscRipper ripperB = BuildRipper(registry: registry, processRunner: runnerB.Object);

        RipRequest requestA = MakeRequest(drivePath: "/dev/sr0", volumeUuid: "vol-uuid-A");
        RipRequest requestB = MakeRequest(drivePath: "/dev/sr1", volumeUuid: "vol-uuid-B");

        Task<DiscRipResult[]> ripA = ripperA.RipAsync(
            request: requestA,
            outputDirectory: "/tmp/ripA",
            ct: CancellationToken.None
        );
        Task<DiscRipResult[]> ripB = ripperB.RipAsync(
            request: requestB,
            outputDirectory: "/tmp/ripB",
            ct: CancellationToken.None
        );

        // Drive B completes independently — runner B is not blocked
        DiscRipResult[] resultsB = await ripB;
        resultsB.Should().HaveCount(expected: 1);
        resultsB[0].Success.Should().BeTrue();

        // Unblock A and verify it also completes
        tcsA.SetResult(result: SuccessResult());
        DiscRipResult[] resultsA = await ripA;
        resultsA.Should().HaveCount(expected: 1);
        resultsA[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task RipAsync_LockReleasedAfterSuccess()
    {
        DriveLockRegistry registry = new();

        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: SuccessResult());

        IDiscRipper ripper = BuildRipper(registry: registry, processRunner: runner.Object);
        RipRequest request = MakeRequest(drivePath: "/dev/sr0", volumeUuid: "vol-uuid-success");

        await ripper.RipAsync(request: request, outputDirectory: "/tmp/rip", ct: CancellationToken.None);

        // Lock must be free — a second call must not throw
        Func<Task> secondRip = () => ripper.RipAsync(request: request, outputDirectory: "/tmp/rip2", ct: CancellationToken.None);
        await secondRip.Should().NotThrowAsync<DiscDriveBusyException>();
    }

    [Fact]
    public async Task RipAsync_LockReleasedAfterFfmpegFailure()
    {
        DriveLockRegistry registry = new();

        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: FailureResult());

        IDiscRipper ripper = BuildRipper(registry: registry, processRunner: runner.Object);
        RipRequest request = MakeRequest(drivePath: "/dev/sr0", volumeUuid: "vol-uuid-fail");

        // RipAsync does not throw on ffmpeg exit ≠ 0 — it returns a failed result
        DiscRipResult[] results = await ripper.RipAsync(
            request: request,
            outputDirectory: "/tmp/rip",
            ct: CancellationToken.None
        );
        results[0].Success.Should().BeFalse();

        // Lock must be free — second call acquires without throwing
        Func<Task> secondRip = () => ripper.RipAsync(request: request, outputDirectory: "/tmp/rip2", ct: CancellationToken.None);
        await secondRip.Should().NotThrowAsync<DiscDriveBusyException>();
    }
}
