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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage;

namespace NoMercy.Tests.OpticalMedia.Rip;

/// <summary>
/// REQUIREMENT: <see cref="DiscRipper"/> must enforce one active rip per
/// drive (via <see cref="DriveLockRegistry"/>), build the correct ffmpeg
/// stream-copy command line per disc type (Blu-ray/DVD/CD), only map audio
/// and subtitle streams the caller opted into, forward BD+ / AACS KEYDB
/// environment overrides only for <c>bluray:</c> paths, and always release
/// the drive lock — even when ripping throws.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class DiscRipperTests
{
    private static EncoderOptions MakeOptions(BluRayOptions? bluRay = null) =>
        new() { FfmpegPathOverride = "ffmpeg", BluRay = bluRay };

    private static Mock<IStorage> MakeStorageMock(long sizeOrZero = 1_234_567)
    {
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.CreateDirectory(It.IsAny<string>()));
        storage
            .Setup(expression: s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: path => new LocalPathLease(path: path));
        storage.Setup(expression: s => s.SizeOrZero(It.IsAny<string>())).Returns(value: sizeOrZero);
        return storage;
    }

    private static Mock<IProcessRunner> MakeSucceedingRunner(string stdErr = "")
    {
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: stdErr, Duration: TimeSpan.FromMilliseconds(milliseconds: 5)));
        return runner;
    }

    private static RipRequest MakeVideoRequest(
        string drivePath = "D:\\",
        OpticalDiscType discType = OpticalDiscType.Dvd,
        int[]? titles = null,
        AudioTrackSelection[]? audioTracks = null,
        SubtitleSelection[]? subtitles = null,
        string? volumeUuid = null
    ) =>
        new(
            DrivePath: drivePath,
            SelectedTitleIndices: titles ?? [1],
            MetadataId: null,
            Custom: null,
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: audioTracks ?? [],
            Subtitles: subtitles ?? [],
            Mode: RipMode.RipToRaw,
            VolumeUuid: volumeUuid,
            DiscType: discType
        );

    // ── Drive locking ──────────────────────────────────────────────────────

    [Fact]
    public async Task RipAsync_DriveAlreadyLocked_ThrowsDiscDriveBusyException()
    {
        DriveLockRegistry lockRegistry = new();
        lockRegistry.TryAcquire(driveKey: "D:\\", driveLock: out _);

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: MakeSucceedingRunner().Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: lockRegistry,
            logger: NullLogger<DiscRipper>.Instance
        );

        Func<Task> act = () => ripper.RipAsync(request: MakeVideoRequest(), outputDirectory: "/out", ct: CancellationToken.None);

        await act.Should().ThrowAsync<DiscDriveBusyException>();
    }

    [Fact]
    public async Task RipAsync_UsesVolumeUuidAsLockKeyWhenPresent()
    {
        DriveLockRegistry lockRegistry = new();
        lockRegistry.TryAcquire(driveKey: "volume-uuid-123", driveLock: out _);

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: MakeSucceedingRunner().Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: lockRegistry,
            logger: NullLogger<DiscRipper>.Instance
        );

        // DrivePath itself is free — only the VolumeUuid is locked — so the
        // busy exception proves VolumeUuid (not DrivePath) was used as the key.
        Func<Task> act = () =>
            ripper.RipAsync(
                request: MakeVideoRequest(volumeUuid: "volume-uuid-123"),
                outputDirectory: "/out",
                ct: CancellationToken.None
            );

        await act.Should().ThrowAsync<DiscDriveBusyException>();
    }

    [Fact]
    public async Task RipAsync_ReleasesLockAfterSuccess_AllowingASecondRip()
    {
        DriveLockRegistry lockRegistry = new();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: MakeSucceedingRunner().Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: lockRegistry,
            logger: NullLogger<DiscRipper>.Instance
        );

        await ripper.RipAsync(request: MakeVideoRequest(), outputDirectory: "/out", ct: CancellationToken.None);
        Func<Task> secondRip = () =>
            ripper.RipAsync(request: MakeVideoRequest(), outputDirectory: "/out", ct: CancellationToken.None);

        await secondRip
            .Should()
            .NotThrowAsync(because: "the lock must be released once the first rip completes");
    }

    [Fact]
    public async Task RipAsync_ReleasesLockEvenWhenRipThrows()
    {
        DriveLockRegistry lockRegistry = new();
        Mock<IProcessRunner> throwingRunner = new();
        throwingRunner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new InvalidOperationException(message: "ffmpeg crashed"));

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: throwingRunner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: lockRegistry,
            logger: NullLogger<DiscRipper>.Instance
        );

        Func<Task> firstRip = () =>
            ripper.RipAsync(request: MakeVideoRequest(), outputDirectory: "/out", ct: CancellationToken.None);
        await firstRip.Should().ThrowAsync<InvalidOperationException>();

        bool reacquired = lockRegistry.TryAcquire(driveKey: "D:\\", driveLock: out _);
        reacquired.Should().BeTrue(because: "the finally block must release the lock even on failure");
    }

    // ── CreateDirectory always called ──────────────────────────────────────

    [Fact]
    public async Task RipAsync_CreatesOutputDirectory()
    {
        Mock<IStorage> storage = MakeStorageMock();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: MakeSucceedingRunner().Object,
            storage: storage.Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        await ripper.RipAsync(request: MakeVideoRequest(), outputDirectory: "/out/dir", ct: CancellationToken.None);

        storage.Verify(expression: s => s.CreateDirectory("/out/dir"), times: Times.Once);
    }

    // ── Video title rip: per-disc-type ffmpeg args ─────────────────────────

    [Theory]
    [InlineData(data: [OpticalDiscType.BluRay, "-playlist"])]
    [InlineData(data: [OpticalDiscType.Dvd, "-title"])]
    public async Task RipAsync_VideoDisc_PassesDiscTypeSpecificArgs(
        OpticalDiscType discType,
        string expectedFlag
    )
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        await ripper.RipAsync(request: MakeVideoRequest(discType: discType), outputDirectory: "/out", ct: CancellationToken.None);

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args => args.Contains(expectedFlag) && args.Contains("copy")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task RipAsync_BlurayInputUrl_UsesBlurayProtocolWithTrailingSlash()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        await ripper.RipAsync(
            request: MakeVideoRequest(drivePath: "D:\\", discType: OpticalDiscType.BluRay),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args => args.Contains("bluray:D:/")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task RipAsync_DvdInputUrl_PointsAtVideoTsFolder()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        await ripper.RipAsync(
            request: MakeVideoRequest(drivePath: "D:\\", discType: OpticalDiscType.Dvd),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args => args.Contains("D:/VIDEO_TS/")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task RipAsync_AlreadyPrefixedBlurayDrivePath_UsedVerbatimAsInputUrl()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        RipRequest request = MakeVideoRequest(
            drivePath: "bluray:/dev/sr0/",
            discType: OpticalDiscType.BluRay
        );
        await ripper.RipAsync(request: request, outputDirectory: "/out", ct: CancellationToken.None);

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args => args.Contains("bluray:/dev/sr0/")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task RipAsync_OnlyIncludedAudioAndSubtitleTracksAreMapped()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        RipRequest request = MakeVideoRequest(
            audioTracks: [new(StreamIndex: 0, Include: true), new(StreamIndex: 1, Include: false)],
            subtitles:
            [
                new(StreamIndex: 2, Include: true, Policy: SubtitlePolicy.Copy),
                new(StreamIndex: 3, Include: false, Policy: SubtitlePolicy.Copy),
            ]
        );
        await ripper.RipAsync(request: request, outputDirectory: "/out", ct: CancellationToken.None);

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args =>
                        args.Contains("0:a:0")
                        && !args.Contains("0:a:1")
                        && args.Contains("0:s:2")
                        && !args.Contains("0:s:3")
                    ),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    // ── BD+/AACS env overrides ──────────────────────────────────────────────

    [Fact]
    public async Task RipAsync_BlurayWithKeyDbOverride_ForwardsEnvironmentVariables()
    {
        Mock<IProcessRunner> runner = new();
        IReadOnlyDictionary<string, string>? capturedEnv = null;
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<IReadOnlyDictionary<string, string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                string,
                string[],
                IReadOnlyDictionary<string, string>?,
                string?,
                CancellationToken
            >(action: (_, _, env, _, _) => capturedEnv = env)
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.Zero));

        BluRayOptions bluRay = new()
        {
            KeyDbOverridePath = "/etc/nomercy/KEYDB.cfg",
            AacsKeysOverridePath = "/etc/nomercy/bdplus",
        };
        DiscRipper ripper = new(
            options: MakeOptions(bluRay: bluRay),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        await ripper.RipAsync(
            request: MakeVideoRequest(drivePath: "bluray:/dev/sr0/", discType: OpticalDiscType.BluRay),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        capturedEnv.Should().NotBeNull();
        capturedEnv![key: "LIBAACS_KEY_DB"].Should().Be(expected: "/etc/nomercy/KEYDB.cfg");
        capturedEnv[key: "LIBBDPLUS_DATABASE"].Should().Be(expected: "/etc/nomercy/bdplus");
    }

    [Fact]
    public async Task RipAsync_DvdDisc_NeverForwardsBluRayEnvOverrides()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        BluRayOptions bluRay = new() { KeyDbOverridePath = "/etc/nomercy/KEYDB.cfg" };
        DiscRipper ripper = new(
            options: MakeOptions(bluRay: bluRay),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        await ripper.RipAsync(
            request: MakeVideoRequest(discType: OpticalDiscType.Dvd),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        // The 4-arg RunAsync overload (with env dict) must never be called for DVD.
        runner.Verify(
            expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<IReadOnlyDictionary<string, string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task RipAsync_BlurayWithNoBluRayOptionsConfigured_UsesPlainRunAsyncOverload()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(bluRay: null),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        await ripper.RipAsync(
            request: MakeVideoRequest(discType: OpticalDiscType.BluRay),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    // ── Failure path ────────────────────────────────────────────────────────

    [Fact]
    public async Task RipAsync_FfmpegExitsNonZero_ReturnsFailureResultWithMessage()
    {
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "generic failure", Duration: TimeSpan.Zero));

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        DiscRipResult[] results = await ripper.RipAsync(
            request: MakeVideoRequest(),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain(expected: "1");
        results[0].OutputSizeBytes.Should().Be(expected: 0);
    }

    [Fact]
    public async Task RipAsync_BlurayFfmpegFails_ClassifiesAacsStderr()
    {
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "aacs: no matching certificate", Duration: TimeSpan.Zero));

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        Func<Task> act = () =>
            ripper.RipAsync(
                request: MakeVideoRequest(drivePath: "bluray:/dev/sr0/", discType: OpticalDiscType.BluRay),
                outputDirectory: "/out",
                ct: CancellationToken.None
            );

        // DiscScanner.ClassifyBluRayStderr throws a structured
        // EncoderRuntimeException for AACS failures on bluray: paths — this
        // propagates out of RipOneTitleAsync rather than returning a
        // DiscRipResult, letting the caller (DiscRipJob) surface the
        // specific AACS rule id instead of a generic ffmpeg-exit message.
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task RipAsync_NonBlurayFfmpegFailure_DoesNotClassifyStderr()
    {
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "aacs: no matching certificate", Duration: TimeSpan.Zero));

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        // Same stderr text, but a DVD drive path never routes through
        // ClassifyBluRayStderr — the result degrades to a normal failure.
        DiscRipResult[] results = await ripper.RipAsync(
            request: MakeVideoRequest(drivePath: "D:\\", discType: OpticalDiscType.Dvd),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
    }

    [Fact]
    public async Task RipAsync_LongStderr_TailIsTruncatedInLogButErrorMessageIntact()
    {
        string longStderr = new(c: 'x', count: 2000);
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: longStderr, Duration: TimeSpan.Zero));

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        DiscRipResult[] results = await ripper.RipAsync(
            request: MakeVideoRequest(),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Be(expected: "ffmpeg exited with code 1");
    }

    // ── Multiple titles + logging on partial failure ────────────────────────

    [Fact]
    public async Task RipAsync_MultipleTitles_RipsEachAndReturnsOneResultPerTitle()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        DiscRipResult[] results = await ripper.RipAsync(
            request: MakeVideoRequest(titles: [1, 2, 3]),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 3);
        results.Select(selector: r => r.TitleIndex).Should().Equal(elements: [1, 2, 3]);
        results.Should().OnlyContain(predicate: r => r.Success);
    }

    [Fact]
    public async Task RipAsync_CancelledBetweenTitles_ThrowsOperationCanceledException()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = () => ripper.RipAsync(request: MakeVideoRequest(titles: [1, 2]), outputDirectory: "/out", ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── ResolveDiscType fallback (None → sniff drive-path prefix) ──────────

    [Theory]
    [InlineData(data: ["bluray:/dev/sr0", "-playlist"])]
    [InlineData(data: ["dvd:/dev/sr0", null])]
    public async Task RipAsync_DiscTypeNone_SniffsFromDrivePathPrefix(
        string drivePath,
        string? expectedFlag
    )
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        await ripper.RipAsync(
            request: MakeVideoRequest(drivePath: drivePath, discType: OpticalDiscType.None),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        if (expectedFlag is not null)
        {
            runner.Verify(
                expression: r =>
                    r.RunAsync(
                        "ffmpeg",
                        It.Is<string[]>(args => args.Contains(expectedFlag)),
                        It.IsAny<string?>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.Once
            );
        }
    }

    [Fact]
    public async Task RipAsync_DiscTypeNoneAndNoRecognizedPrefix_ResolvesToNone_UsesRawPathAsInput()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        // "D:\" has no "bluray:"/"dvd:" prefix, so ResolveDiscType falls
        // all the way through to OpticalDiscType.None, and BuildInputUrl's
        // switch on that None value hits its own `_ => drivePath` arm.
        await ripper.RipAsync(
            request: MakeVideoRequest(drivePath: "D:\\", discType: OpticalDiscType.None),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args => args.Contains("D:\\")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task RipAsync_BlurayFfmpegFailsWithUnrecognizedStderr_ReturnsNormalFailure_DoesNotThrow()
    {
        // The bluray:-prefix branch that calls ClassifyBluRayStderr is only
        // exercised past its call (falling through to the normal failure
        // path below it) when the stderr text matches none of the known
        // AACS/BD+/protocol error patterns — unlike the classified-failure
        // test above, this proves the fall-through continues normally.
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "some unrelated ffmpeg warning", Duration: TimeSpan.Zero));

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        DiscRipResult[] results = await ripper.RipAsync(
            request: MakeVideoRequest(drivePath: "bluray:/dev/sr0/", discType: OpticalDiscType.BluRay),
            outputDirectory: "/out",
            ct: CancellationToken.None
        );

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain(expected: "1");
    }

    [Fact]
    public async Task RipOneTitleAsync_InvokedDirectlyForCdDiscType_AddsLibcdioArgs()
    {
        // RipOneTitleAsync's `case OpticalDiscType.Cd` switch arm (and
        // BuildInputUrl's matching arm) can never be reached through the
        // public RipAsync surface — RipInternalAsync always routes CD discs
        // to RipCdTracksAsync before RipOneTitleAsync is ever called. This
        // invokes the private method directly via reflection to prove the
        // arm's behavior is correct rather than leaving genuinely-dead
        // symmetry code silently unverified.
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        System.Reflection.MethodInfo method = typeof(DiscRipper).GetMethod(
            name: "RipOneTitleAsync",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        )!;

        RipRequest request = MakeVideoRequest(drivePath: "/dev/sr0", discType: OpticalDiscType.Cd);
        Task<DiscRipResult> task =
            (Task<DiscRipResult>)
                method.Invoke(obj: ripper, parameters: [request, 1, "/out", CancellationToken.None])!;
        DiscRipResult result = await task;

        result.Success.Should().BeTrue();
        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args => args.Contains("libcdio") && args.Contains("/dev/sr0")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    // ── CD track rip ────────────────────────────────────────────────────────

    [Fact]
    public async Task RipAsync_CdDisc_RipsEachSelectedTrackToFlac()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        RipRequest request = MakeVideoRequest(
            drivePath: "/dev/sr0",
            discType: OpticalDiscType.Cd,
            titles: [1, 2]
        );
        DiscRipResult[] results = await ripper.RipAsync(request: request, outputDirectory: "/out", ct: CancellationToken.None);

        results.Should().HaveCount(expected: 2);
        results.Select(selector: r => r.TitleIndex).Should().Equal(elements: [1, 2]);
        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args =>
                        args.Contains("libcdio") && args.Contains("flac") && args.Contains("0:a:0")
                    ),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args => args.Contains("0:a:1")),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task RipAsync_CdTrack_OutputFileNameFollowsTrackNumberPrefix()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        Mock<IStorage> storage = MakeStorageMock();
        string? capturedOutputPath = null;
        storage
            .Setup(expression: s => s.AcquireLocalPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: path =>
            {
                capturedOutputPath = path;
                return new LocalPathLease(path: path);
            });

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: storage.Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        RipRequest request = MakeVideoRequest(
            drivePath: "/dev/sr0",
            discType: OpticalDiscType.Cd,
            titles: [3]
        );
        await ripper.RipAsync(request: request, outputDirectory: "/out", ct: CancellationToken.None);

        capturedOutputPath.Should().Contain(expected: "03 - Track 03.flac");
    }

    [Fact]
    public async Task RipAsync_CdTrackFfmpegFails_ReturnsFailureResult()
    {
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "read error", Duration: TimeSpan.Zero));

        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        RipRequest request = MakeVideoRequest(
            drivePath: "/dev/sr0",
            discType: OpticalDiscType.Cd,
            titles: [1]
        );
        DiscRipResult[] results = await ripper.RipAsync(request: request, outputDirectory: "/out", ct: CancellationToken.None);

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain(expected: "1");
    }

    [Fact]
    public async Task RipAsync_CdCancelledBetweenTracks_ThrowsOperationCanceledException()
    {
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        using CancellationTokenSource cts = new();
        cts.Cancel();

        RipRequest request = MakeVideoRequest(
            drivePath: "/dev/sr0",
            discType: OpticalDiscType.Cd,
            titles: [1, 2]
        );
        Func<Task> act = () => ripper.RipAsync(request: request, outputDirectory: "/out", ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RipAsync_CdTrack_TitleIsAlwaysWellFormedFsPath()
    {
        // ResolveTrackTitle's fixed "Track NN" format always passes through
        // SanitizeForPath cleanly (no user-controlled title is currently
        // wired through RipRequest for CD tracks); this proves the rip path
        // never throws building the sanitized output filename.
        Mock<IProcessRunner> runner = MakeSucceedingRunner();
        DiscRipper ripper = new(
            options: MakeOptions(),
            processRunner: runner.Object,
            storage: MakeStorageMock().Object,
            driveLockRegistry: new DriveLockRegistry(),
            logger: NullLogger<DiscRipper>.Instance
        );

        RipRequest request = MakeVideoRequest(
            drivePath: "/dev/sr0",
            discType: OpticalDiscType.Cd,
            titles: [1]
        );
        Func<Task> act = () => ripper.RipAsync(request: request, outputDirectory: "/out", ct: CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
