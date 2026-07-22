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
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.DiscRipping;

/// <summary>
/// Verifies the CD-specific rip path in <see cref="DiscRipper"/>.
/// CD-DA tracks must use <c>-f libcdio</c> + <c>-map 0:a:N</c> + <c>-c:a flac</c>.
/// The shared video path (<c>-map 0:v:0</c> + <c>-c copy</c> + <c>.mkv</c>) must
/// NOT appear in any CD rip invocation.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class DiscRipperCdTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly EncoderOptions _options = new()
    {
        FfmpegPathOverride = "/usr/bin/ffmpeg",
        FfprobePathOverride = "/usr/bin/ffprobe",
    };
    private readonly List<string[]> _capturedArgs = [];

    public DiscRipperCdTests()
    {
        _outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"CdRip_{Guid.NewGuid():N}");

        _processRunner
            .Setup(expression: runner =>
                runner.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, args, _, _) => _capturedArgs.Add(item: args)
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.FromSeconds(seconds: 1)));
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDir))
            Directory.Delete(path: _outputDir, recursive: true);
        GC.SuppressFinalize(obj: this);
    }

    // ── Format flags ──────────────────────────────────────────────────────

    [Fact]
    public async Task RipAsync_Cd_UsesLibcdioInputFormat()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [1]);

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        _capturedArgs.Should().HaveCount(expected: 1);
        string[] args = _capturedArgs[index: 0];
        int fIdx = Array.IndexOf(array: args, value: "-f");
        fIdx.Should().BeGreaterThanOrEqualTo(expected: 0, because: "CD rip must pass -f");
        args[fIdx + 1].Should().Be(expected: "libcdio");
    }

    [Fact]
    public async Task RipAsync_Cd_EncodesFlac()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [1]);

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        string[] args = _capturedArgs[index: 0];
        int caIdx = Array.IndexOf(array: args, value: "-c:a");
        caIdx.Should().BeGreaterThanOrEqualTo(expected: 0, because: "CD rip must pass -c:a");
        args[caIdx + 1].Should().Be(expected: "flac");
    }

    [Fact]
    public async Task RipAsync_Cd_OutputPathEndsWithFlac()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [1]);

        DiscRipResult[] results = await ripper.RipAsync(
            request: request,
            outputDirectory: _outputDir,
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 1);
        results[0].OutputPath.Should().EndWith(expected: ".flac");
    }

    // ── -map 0:v:0 must NOT appear ────────────────────────────────────────

    [Fact]
    public async Task RipAsync_Cd_DoesNotMapVideoStream()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [1]);

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        string[] args = _capturedArgs[index: 0];
        args.Should().NotContain(unexpected: "0:v:0", because: "CD-DA has no video stream");
    }

    [Fact]
    public async Task RipAsync_Cd_DoesNotUseMkvContainer()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [3]);

        DiscRipResult[] results = await ripper.RipAsync(
            request: request,
            outputDirectory: _outputDir,
            ct: CancellationToken.None
        );

        results[0].OutputPath.Should().NotEndWith(unexpected: ".mkv");
    }

    // ── Per-track stream mapping ──────────────────────────────────────────

    [Fact]
    public async Task RipAsync_Cd_Track1_MapsToStream0()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [1]);

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        string[] args = _capturedArgs[index: 0];
        int mapIdx = Array.IndexOf(array: args, value: "-map");
        mapIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[mapIdx + 1].Should().Be(expected: "0:a:0", because: "track 1 = stream index 0");
    }

    [Fact]
    public async Task RipAsync_Cd_Track5_MapsToStream4()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [5]);

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        string[] args = _capturedArgs[index: 0];
        int mapIdx = Array.IndexOf(array: args, value: "-map");
        args[mapIdx + 1].Should().Be(expected: "0:a:4", because: "track 5 = stream index 4");
    }

    // ── One invocation per track ──────────────────────────────────────────

    [Fact]
    public async Task RipAsync_Cd_MultipleTracksFireSeparateInvocations()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [1, 2, 3]);

        DiscRipResult[] results = await ripper.RipAsync(
            request: request,
            outputDirectory: _outputDir,
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 3);
        _capturedArgs.Should().HaveCount(expected: 3, because: "one ffmpeg call per CD track");
    }

    [Fact]
    public async Task RipAsync_Cd_EachInvocationMapsCorrectStream()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [2, 4]);

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        _capturedArgs.Should().HaveCount(expected: 2);
        int mapIdx0 = Array.IndexOf(array: _capturedArgs[index: 0], value: "-map");
        int mapIdx1 = Array.IndexOf(array: _capturedArgs[index: 1], value: "-map");
        _capturedArgs[index: 0][mapIdx0 + 1].Should().Be(expected: "0:a:1", because: "track 2 = stream 1");
        _capturedArgs[index: 1][mapIdx1 + 1].Should().Be(expected: "0:a:3", because: "track 4 = stream 3");
    }

    // ── Output file naming ────────────────────────────────────────────────

    [Fact]
    public async Task RipAsync_Cd_OutputFileNameContainsZeroPaddedTrackNumber()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [3]);

        DiscRipResult[] results = await ripper.RipAsync(
            request: request,
            outputDirectory: _outputDir,
            ct: CancellationToken.None
        );

        string fileName = Path.GetFileName(path: results[0].OutputPath);
        fileName.Should().StartWith(expected: "03", because: "track number is zero-padded to 2 digits");
    }

    // ── Failure propagation ───────────────────────────────────────────────

    [Fact]
    public async Task RipAsync_Cd_FfmpegFailure_ReturnsFailureResult()
    {
        _processRunner.Reset();
        _processRunner
            .Setup(expression: runner =>
                runner.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "I/O error", Duration: TimeSpan.FromSeconds(seconds: 1)));

        DiscRipper ripper = BuildRipper();
        RipRequest request = CdRequest(drivePath: "/dev/sr0", trackIndices: [1]);

        DiscRipResult[] results = await ripper.RipAsync(
            request: request,
            outputDirectory: _outputDir,
            ct: CancellationToken.None
        );

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain(expected: "exited with code 1");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private DiscRipper BuildRipper()
    {
        LocalStorageDriver driver = new();
        LocalStorage storage = new(driver: driver, guard: new(allowedRoots: [], driver: driver));
        return new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: storage,
            driveLockRegistry: new(),
            logger: NullLogger<DiscRipper>.Instance
        );
    }

    private static RipRequest CdRequest(string drivePath, int[] trackIndices) =>
        new(
            DrivePath: drivePath,
            SelectedTitleIndices: trackIndices,
            MetadataId: null,
            Custom: null,
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: [],
            DiscType: OpticalDiscType.Cd
        );
}
