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

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.ContentAnalysis;

public class CropDetectorTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly EncoderOptions _options = new()
    {
        FfmpegPathOverride = "ffmpeg",
        FfprobePathOverride = "ffprobe",
    };

    [Fact]
    public async Task Detect_StableCrop_ReturnsWithShouldCropTrue()
    {
        string[] stderrLines =
        [
            "frame=1 fps=0 q=-0 size=N/A time=00:00:00.00 bitrate=N/A",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
            "[Parsed_cropdetect_0 @ 0x7] x1:0 x2:1919 y1:20 y2:1059 w:1920 h:1040 x:0 y:20 crop=1920:1040:0:20",
        ];

        SetupStderr(lines: stderrLines, exitCode: 0);
        CropDetector detector = new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync(inputPath: "/tmp/in.mkv", ct: CancellationToken.None);

        Assert.Equal(expected: 1920, actual: result.Width);
        Assert.Equal(expected: 1040, actual: result.Height);
        Assert.Equal(expected: 0, actual: result.X);
        Assert.Equal(expected: 20, actual: result.Y);
        Assert.True(condition: result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_FullFrameCrop_ShouldCropFalse()
    {
        string[] stderrLines = Enumerable.Repeat(element: "[cropdetect] crop=1920:1080:0:0", count: 10).ToArray();

        SetupStderr(lines: stderrLines, exitCode: 0);
        CropDetector detector = new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync(inputPath: "/tmp/in.mkv", ct: CancellationToken.None);

        Assert.False(condition: result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_FewerThanMinObservations_ShouldCropFalse()
    {
        string[] stderrLines = Enumerable
            .Repeat(element: "crop=1920:1040:0:20", count: 3) // below threshold
            .ToArray();

        SetupStderr(lines: stderrLines, exitCode: 0);
        CropDetector detector = new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync(inputPath: "/tmp/in.mkv", ct: CancellationToken.None);

        Assert.False(condition: result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_FfmpegNonZeroExit_ReturnsEmptyResult()
    {
        SetupStderr(lines: ["crop=1920:1040:0:20"], exitCode: 1);
        CropDetector detector = new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync(inputPath: "/tmp/in.mkv", ct: CancellationToken.None);

        Assert.Equal(expected: 0, actual: result.Width);
        Assert.Equal(expected: 0, actual: result.Height);
        Assert.False(condition: result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_PicksMostFrequentCrop()
    {
        // 6 x wide-crop, 2 x no-crop → wide wins
        string[] stderrLines =
        [
            "crop=1920:1040:0:20",
            "crop=1920:1040:0:20",
            "crop=1920:1040:0:20",
            "crop=1920:1080:0:0",
            "crop=1920:1040:0:20",
            "crop=1920:1040:0:20",
            "crop=1920:1080:0:0",
            "crop=1920:1040:0:20",
        ];

        SetupStderr(lines: stderrLines, exitCode: 0);
        CropDetector detector = new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync(inputPath: "/tmp/in.mkv", ct: CancellationToken.None);

        Assert.Equal(expected: 1040, actual: result.Height);
        Assert.Equal(expected: 20, actual: result.Y);
        Assert.True(condition: result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_UsesSpecCompliantSampleWindow()
    {
        // Phase 4.1 spec: -ss 60, -t 180, cropdetect round=4.
        string[]? capturedArgs = null;
        _processRunner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                valueFunction: (
                    string _,
                    string[] args,
                    Action<string>? _,
                    Action<string>? _,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    capturedArgs = args;
                    return Task.FromResult(
                        result: new ProcessResult(ExitCode: 0, StdOut: string.Empty, StdErr: string.Empty, Duration: TimeSpan.Zero)
                    );
                }
            );

        CropDetector detector = new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<CropDetector>.Instance
        );

        await detector.DetectAsync(inputPath: "/tmp/in.mkv", ct: CancellationToken.None);

        Assert.NotNull(@object: capturedArgs);
        // -ss 60
        int ssIdx = Array.IndexOf(array: capturedArgs, value: "-ss");
        Assert.True(condition: ssIdx >= 0);
        Assert.Equal(expected: "60", actual: capturedArgs[ssIdx + 1]);
        // -t 180
        int tIdx = Array.IndexOf(array: capturedArgs, value: "-t");
        Assert.True(condition: tIdx >= 0);
        Assert.Equal(expected: "180", actual: capturedArgs[tIdx + 1]);
        // cropdetect round=4
        Assert.Contains(collection: capturedArgs, filter: a => a.Contains(value: "round=4"));
    }

    [Fact]
    public async Task Detect_PopulatesSampleFramesAnalyzed_AndConfidence()
    {
        // 6 x wide-crop, 2 x no-crop → confidence = 6/8 = 0.75, frames = 6.
        string[] stderrLines =
        [
            "crop=1920:1040:0:20",
            "crop=1920:1040:0:20",
            "crop=1920:1040:0:20",
            "crop=1920:1080:0:0",
            "crop=1920:1040:0:20",
            "crop=1920:1040:0:20",
            "crop=1920:1080:0:0",
            "crop=1920:1040:0:20",
        ];

        SetupStderr(lines: stderrLines, exitCode: 0);
        CropDetector detector = new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<CropDetector>.Instance
        );

        Guid videoFileId = Guid.NewGuid();
        CropResult result = await detector.DetectAsync(
            inputPath: "/tmp/in.mkv",
            sourceVideoFileId: videoFileId,
            ct: CancellationToken.None
        );

        Assert.Equal(expected: videoFileId, actual: result.SourceVideoFileId);
        Assert.Equal(expected: 6, actual: result.SampleFramesAnalyzed);
        Assert.Equal(expected: 6.0 / 8.0, actual: result.Confidence, precision: 3);
        Assert.True(condition: result.ShouldCrop);
    }

    [Fact]
    public async Task Detect_BelowMinObservations_StillReportsFramesAndConfidence()
    {
        // 3 observations of one crop → below the MinObservations gate but
        // SampleFramesAnalyzed + Confidence should still be filled in for UI.
        string[] stderrLines = Enumerable.Repeat(element: "crop=1920:1040:0:20", count: 3).ToArray();

        SetupStderr(lines: stderrLines, exitCode: 0);
        CropDetector detector = new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<CropDetector>.Instance
        );

        CropResult result = await detector.DetectAsync(
            inputPath: "/tmp/in.mkv",
            sourceVideoFileId: null,
            ct: CancellationToken.None
        );

        Assert.False(condition: result.ShouldCrop);
        Assert.Equal(expected: 3, actual: result.SampleFramesAnalyzed);
        Assert.Equal(expected: 1.0, actual: result.Confidence);
    }

    [Fact]
    public async Task Detect_HdrSource_UsesHdrCropLimit()
    {
        // HDR/PQ black bars sit far above the SDR limit=24, so an HDR source
        // MUST raise the cropdetect limit or the letterbox is never detected
        // and gets baked into a stream-copy. Regression guard for the exact
        // failure seen live (cropdetect → 3840x2160:0:0 on an HDR10 source).
        StrongBox<string[]?> args = CaptureCropDetectArgs();
        CropDetector detector = NewDetector();

        await detector.DetectAsync(
            inputPath: "/tmp/in.mkv",
            sourceVideoFileId: null,
            sourceIsHdr: true,
            ct: CancellationToken.None
        );

        Assert.NotNull(@object: args.Value);
        Assert.Contains(collection: args.Value!, filter: a => a.Contains(value: "cropdetect") && a.Contains(value: "limit=128"));
        Assert.DoesNotContain(collection: args.Value!, filter: a => a.Contains(value: "limit=24"));
    }

    [Fact]
    public async Task Detect_SdrSource_UsesSdrCropLimit()
    {
        StrongBox<string[]?> args = CaptureCropDetectArgs();
        CropDetector detector = NewDetector();

        await detector.DetectAsync(
            inputPath: "/tmp/in.mkv",
            sourceVideoFileId: null,
            sourceIsHdr: false,
            ct: CancellationToken.None
        );

        Assert.NotNull(@object: args.Value);
        Assert.Contains(collection: args.Value!, filter: a => a.Contains(value: "cropdetect") && a.Contains(value: "limit=24"));
        Assert.DoesNotContain(collection: args.Value!, filter: a => a.Contains(value: "limit=128"));
    }

    [Fact]
    public async Task Detect_NullHdrFlag_ProbesPqTransfer_UsesHdrLimit()
    {
        // Caller doesn't know the transfer (content-analysis API): the detector
        // probes color_transfer itself. A PQ transfer must select the HDR limit.
        SetupTransferProbe(transfer: "smpte2084");
        StrongBox<string[]?> args = CaptureCropDetectArgs();
        CropDetector detector = NewDetector();

        await detector.DetectAsync(
            inputPath: "/tmp/in.mkv",
            sourceVideoFileId: null,
            sourceIsHdr: null,
            ct: CancellationToken.None
        );

        Assert.NotNull(@object: args.Value);
        Assert.Contains(collection: args.Value!, filter: a => a.Contains(value: "cropdetect") && a.Contains(value: "limit=128"));
    }

    [Fact]
    public async Task Detect_NullHdrFlag_ProbesSdrTransfer_UsesSdrLimit()
    {
        SetupTransferProbe(transfer: "bt709");
        StrongBox<string[]?> args = CaptureCropDetectArgs();
        CropDetector detector = NewDetector();

        await detector.DetectAsync(
            inputPath: "/tmp/in.mkv",
            sourceVideoFileId: null,
            sourceIsHdr: null,
            ct: CancellationToken.None
        );

        Assert.NotNull(@object: args.Value);
        Assert.Contains(collection: args.Value!, filter: a => a.Contains(value: "cropdetect") && a.Contains(value: "limit=24"));
    }

    private CropDetector NewDetector() =>
        new(
            options: _options,
            processRunner: _processRunner.Object,
            storage: TestStorageFactory.CreateLocal(),
            logger: NullLogger<CropDetector>.Instance
        );

    private void SetupTransferProbe(string transfer) =>
        _processRunner
            .Setup(expression: r =>
                r.RunAsync(
                    "ffprobe",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: transfer + "\n", StdErr: string.Empty, Duration: TimeSpan.Zero));

    /// <summary>
    /// Wires the cropdetect (stderr) process call to capture its argument array
    /// into the returned box, filled when the call fires.
    /// </summary>
    private StrongBox<string[]?> CaptureCropDetectArgs()
    {
        StrongBox<string[]?> box = new(value: null);
        _processRunner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                valueFunction: (
                    string _,
                    string[] args,
                    Action<string>? _,
                    Action<string>? _,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    box.Value = args;
                    return Task.FromResult(
                        result: new ProcessResult(ExitCode: 0, StdOut: string.Empty, StdErr: string.Empty, Duration: TimeSpan.Zero)
                    );
                }
            );
        return box;
    }

    private void SetupStderr(string[] lines, int exitCode)
    {
        _processRunner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                valueFunction: (
                    string _,
                    string[] _,
                    Action<string>? onStdOut,
                    Action<string>? onStdErr,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    foreach (string line in lines)
                        onStdErr?.Invoke(obj: line);

                    return Task.FromResult(
                        result: new ProcessResult(
                            ExitCode: exitCode,
                            StdOut: string.Empty,
                            StdErr: string.Join(separator: '\n', value: lines),
                            Duration: TimeSpan.FromSeconds(seconds: 1)
                        )
                    );
                }
            );
    }
}
