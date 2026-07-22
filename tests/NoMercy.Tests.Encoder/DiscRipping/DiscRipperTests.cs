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
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.DiscRipping;

/// <summary>
/// Verifies the FFmpeg command <see cref="DiscRipper"/> builds for each
/// disc type and track-selection combination. The ripper shells out to
/// FFmpeg via <see cref="IProcessRunner"/>; we intercept that call to
/// inspect the argv without running a real process.
/// </summary>
public class DiscRipperTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly EncoderOptions _options = new()
    {
        FfmpegPathOverride = "/usr/bin/ffmpeg",
        FfprobePathOverride = "/usr/bin/ffprobe",
    };
    private readonly List<string[]> _capturedArgs = [];

    public DiscRipperTests()
    {
        _outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"Rip_{Guid.NewGuid():N}");

        _processRunner
            .Setup(expression: r =>
                r.RunAsync(
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

    [Fact]
    public async Task RipAsync_Bluray_UsesPlaylistFlagForTitle()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = Request(
            drivePath: "bluray:/dev/sr0",
            titles: [0],
            audioTracks: [new(StreamIndex: 0, Include: true)],
            subtitles: []
        );

        DiscRipResult[] results = await ripper.RipAsync(
            request: request,
            outputDirectory: _outputDir,
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 1);
        results[0].Success.Should().BeTrue();
        _capturedArgs.Should().HaveCount(expected: 1);
        _capturedArgs[index: 0].Should().Contain(expected: "-playlist").And.Contain(expected: "0");
        _capturedArgs[index: 0].Should().Contain(expected: "bluray:/dev/sr0");
    }

    [Fact]
    public async Task RipAsync_Dvd_DoesNotAddPlaylistFlag()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = Request(
            drivePath: "D:\\",
            titles: [0],
            audioTracks: [new(StreamIndex: 0, Include: true)],
            subtitles: []
        );

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        _capturedArgs[index: 0].Should().NotContain(unexpected: "-playlist");
        _capturedArgs[index: 0].Should().Contain(expected: "D:\\");
    }

    [Fact]
    public async Task RipAsync_OnlyIncludesSelectedAudioTracks()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = Request(
            drivePath: "bluray:/dev/sr0",
            titles: [0],
            audioTracks: [new(StreamIndex: 0, Include: true), new(StreamIndex: 1, Include: false), new(StreamIndex: 2, Include: true)],
            subtitles: []
        );

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        string[] args = _capturedArgs[index: 0];
        // Two -map entries for audio (stream 0 and 2) + one for video.
        args.Where(predicate: a => a == "-map").Should().HaveCount(expected: 3);
        args.Should().Contain(expected: "0:a:0").And.Contain(expected: "0:a:2");
        args.Should().NotContain(unexpected: "0:a:1");
    }

    [Fact]
    public async Task RipAsync_IncludesOnlySelectedSubtitles()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = Request(
            drivePath: "bluray:/dev/sr0",
            titles: [0],
            audioTracks: [new(StreamIndex: 0, Include: true)],
            subtitles: [new(StreamIndex: 0, Include: true, Policy: SubtitlePolicy.Copy), new(StreamIndex: 1, Include: false, Policy: SubtitlePolicy.Copy)]
        );

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        string[] args = _capturedArgs[index: 0];
        args.Should().Contain(expected: "0:s:0");
        args.Should().NotContain(unexpected: "0:s:1");
    }

    [Fact]
    public async Task RipAsync_StreamCopyPreservesQuality()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = Request(
            drivePath: "D:\\",
            titles: [0],
            audioTracks: [new(StreamIndex: 0, Include: true)],
            subtitles: []
        );

        await ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: CancellationToken.None);

        string[] args = _capturedArgs[index: 0];
        int cIdx = Array.IndexOf(array: args, value: "-c");
        cIdx.Should().BeGreaterThanOrEqualTo(expected: 0);
        args[cIdx + 1].Should().Be(expected: "copy");
    }

    [Fact]
    public async Task RipAsync_WritesTitleNumberedOutputPath()
    {
        DiscRipper ripper = BuildRipper();
        RipRequest request = Request(
            drivePath: "bluray:/dev/sr0",
            titles: [3, 7],
            audioTracks: [new(StreamIndex: 0, Include: true)],
            subtitles: []
        );

        DiscRipResult[] results = await ripper.RipAsync(
            request: request,
            outputDirectory: _outputDir,
            ct: CancellationToken.None
        );

        results.Should().HaveCount(expected: 2);
        results[0].OutputPath.Should().EndWith(expected: "title_03.mkv");
        results[1].OutputPath.Should().EndWith(expected: "title_07.mkv");
    }

    [Fact]
    public async Task RipAsync_FfmpegFailure_ReturnsFailureResult()
    {
        _processRunner.Reset();
        _processRunner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "disc read error", Duration: TimeSpan.FromSeconds(seconds: 1)));

        DiscRipper ripper = BuildRipper();
        RipRequest request = Request(
            drivePath: "bluray:/dev/sr0",
            titles: [0],
            audioTracks: [new(StreamIndex: 0, Include: true)],
            subtitles: []
        );

        DiscRipResult[] results = await ripper.RipAsync(
            request: request,
            outputDirectory: _outputDir,
            ct: CancellationToken.None
        );

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain(expected: "exited with code 1");
    }

    [Fact]
    public async Task RipAsync_CancellationToken_StopsLoopBetweenTitles()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DiscRipper ripper = BuildRipper();
        RipRequest request = Request(
            drivePath: "bluray:/dev/sr0",
            titles: [0, 1, 2],
            audioTracks: [new(StreamIndex: 0, Include: true)],
            subtitles: []
        );

        await Assert.ThrowsAsync<OperationCanceledException>(testCode: () =>
            ripper.RipAsync(request: request, outputDirectory: _outputDir, ct: cts.Token)
        );

        _capturedArgs.Should().BeEmpty();
    }

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

    private static RipRequest Request(
        string drivePath,
        int[] titles,
        AudioTrackSelection[] audioTracks,
        SubtitleSelection[] subtitles
    ) =>
        new(
            DrivePath: drivePath,
            SelectedTitleIndices: titles,
            MetadataId: null,
            Custom: null,
            LibraryId: Ulid.NewUlid(),
            FolderId: Ulid.NewUlid(),
            EncodingProfileId: null,
            AudioTracks: audioTracks,
            Subtitles: subtitles
        );
}
