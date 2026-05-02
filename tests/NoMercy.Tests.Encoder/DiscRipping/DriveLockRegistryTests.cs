using Microsoft.Extensions.Logging;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;
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

        bool acquired = registry.TryAcquire("drive-uuid-1", out DriveLock? lock1);

        acquired.Should().BeTrue();
        lock1.Should().NotBeNull();
        lock1!.Dispose();
    }

    [Fact]
    public void TryAcquire_SecondCallerSameDrive_ReturnsFalse()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire("drive-uuid-1", out DriveLock? lock1);

        bool acquired = registry.TryAcquire("drive-uuid-1", out DriveLock? lock2);

        acquired.Should().BeFalse();
        lock2.Should().BeNull();
        lock1!.Dispose();
    }

    [Fact]
    public void TryAcquire_TwoCallersDifferentDrives_BothSucceed()
    {
        DriveLockRegistry registry = new();

        bool acquired1 = registry.TryAcquire("drive-uuid-A", out DriveLock? lock1);
        bool acquired2 = registry.TryAcquire("drive-uuid-B", out DriveLock? lock2);

        acquired1.Should().BeTrue();
        acquired2.Should().BeTrue();
        lock1!.Dispose();
        lock2!.Dispose();
    }

    [Fact]
    public void TryAcquire_AfterRelease_Succeeds()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire("drive-uuid-1", out DriveLock? lock1);
        lock1!.Dispose();

        bool acquired = registry.TryAcquire("drive-uuid-1", out DriveLock? lock2);

        acquired.Should().BeTrue();
        lock2!.Dispose();
    }

    [Fact]
    public void DriveLock_DisposeIsIdempotent()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire("drive-uuid-1", out DriveLock? lock1);

        lock1!.Dispose();
        Action second = () => lock1.Dispose();
        second.Should().NotThrow();

        // After double-dispose, the drive must be acquirable again
        bool acquired = registry.TryAcquire("drive-uuid-1", out DriveLock? lock2);
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
        storageMock.Setup(s => s.CreateDirectory(It.IsAny<string>()));
        storageMock
            .Setup(s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns((string p) => new LocalPathLease(p));
        storageMock.Setup(s => s.SizeOrZero(It.IsAny<string>())).Returns(1024L);

        Mock<ILogger<DiscRipper>> loggerMock = new();

        return new DiscRipper(opts, processRunner, storageMock.Object, registry, loggerMock.Object);
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
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => tcs.Task);

        Mock<IProcessRunner> runner2 = new();

        IDiscRipper ripper1 = BuildRipper(registry, runner1.Object);
        IDiscRipper ripper2 = BuildRipper(registry, runner2.Object);

        RipRequest request = MakeRequest("/dev/sr0", volumeUuid: "vol-uuid-same");

        Task<DiscRipResult[]> rip1 = ripper1.RipAsync(request, "/tmp/rip1", CancellationToken.None);

        // Give the first rip time to acquire the lock
        await Task.Delay(50);

        Func<Task> rip2 = () => ripper2.RipAsync(request, "/tmp/rip2", CancellationToken.None);
        await rip2.Should().ThrowAsync<DiscDriveBusyException>();

        // Unblock rip1 and let it finish cleanly
        tcs.SetResult(SuccessResult());
        await rip1;
    }

    [Fact]
    public async Task RipAsync_DifferentDrivesConcurrently_BothProceed()
    {
        DriveLockRegistry registry = new();

        TaskCompletionSource<ProcessResult> tcsA = new();
        Mock<IProcessRunner> runnerA = new();
        runnerA
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => tcsA.Task);

        Mock<IProcessRunner> runnerB = new();
        runnerB
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SuccessResult());

        IDiscRipper ripperA = BuildRipper(registry, runnerA.Object);
        IDiscRipper ripperB = BuildRipper(registry, runnerB.Object);

        RipRequest requestA = MakeRequest("/dev/sr0", volumeUuid: "vol-uuid-A");
        RipRequest requestB = MakeRequest("/dev/sr1", volumeUuid: "vol-uuid-B");

        Task<DiscRipResult[]> ripA = ripperA.RipAsync(
            requestA,
            "/tmp/ripA",
            CancellationToken.None
        );
        Task<DiscRipResult[]> ripB = ripperB.RipAsync(
            requestB,
            "/tmp/ripB",
            CancellationToken.None
        );

        // Drive B completes independently — runner B is not blocked
        DiscRipResult[] resultsB = await ripB;
        resultsB.Should().HaveCount(1);
        resultsB[0].Success.Should().BeTrue();

        // Unblock A and verify it also completes
        tcsA.SetResult(SuccessResult());
        DiscRipResult[] resultsA = await ripA;
        resultsA.Should().HaveCount(1);
        resultsA[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task RipAsync_LockReleasedAfterSuccess()
    {
        DriveLockRegistry registry = new();

        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SuccessResult());

        IDiscRipper ripper = BuildRipper(registry, runner.Object);
        RipRequest request = MakeRequest("/dev/sr0", volumeUuid: "vol-uuid-success");

        await ripper.RipAsync(request, "/tmp/rip", CancellationToken.None);

        // Lock must be free — a second call must not throw
        Func<Task> secondRip = () => ripper.RipAsync(request, "/tmp/rip2", CancellationToken.None);
        await secondRip.Should().NotThrowAsync<DiscDriveBusyException>();
    }

    [Fact]
    public async Task RipAsync_LockReleasedAfterFfmpegFailure()
    {
        DriveLockRegistry registry = new();

        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(FailureResult());

        IDiscRipper ripper = BuildRipper(registry, runner.Object);
        RipRequest request = MakeRequest("/dev/sr0", volumeUuid: "vol-uuid-fail");

        // RipAsync does not throw on ffmpeg exit ≠ 0 — it returns a failed result
        DiscRipResult[] results = await ripper.RipAsync(
            request,
            "/tmp/rip",
            CancellationToken.None
        );
        results[0].Success.Should().BeFalse();

        // Lock must be free — second call acquires without throwing
        Func<Task> secondRip = () => ripper.RipAsync(request, "/tmp/rip2", CancellationToken.None);
        await secondRip.Should().NotThrowAsync<DiscDriveBusyException>();
    }
}
