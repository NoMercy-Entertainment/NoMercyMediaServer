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

using Moq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Strategies.Mp4;

public class Mp4ChapterTests : IDisposable
{
    private readonly string _tempDir;

    public Mp4ChapterTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"Mp4ChapterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempDir);

        // Write a dummy "output.mp4" so the rename in FinalizeAsync succeeds
        File.WriteAllText(path: Path.Combine(path1: _tempDir, path2: "output.mp4"), contents: "dummy");
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    private static readonly IReadOnlyList<ChapterInfo> ThreeChapters =
    [
        new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 10), Title: "Opening"),
        new(Start: TimeSpan.FromMinutes(minutes: 10), End: TimeSpan.FromMinutes(minutes: 50), Title: "Act One"),
        new(Start: TimeSpan.FromMinutes(minutes: 50), End: TimeSpan.FromMinutes(minutes: 90), Title: "Finale"),
    ];

    // ------------------------------------------------------------------
    // ffmeta file content
    // ------------------------------------------------------------------

    [Fact]
    public async Task FinalizeAsync_WithChapters_WritesFfmetaFile()
    {
        Mock<IFfmpegExecutor> executorMock = BuildExecutorMock();
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal(), ffmpegExecutor: executorMock.Object);

        await strategy.FinalizeAsync(outputDirectory: _tempDir, plan: CreatePlanWithChapters(), mediaTitle: "Movie", ct: default);

        // ffmeta is deleted after re-mux; but the executor was called — verified below.
        // The content test uses a second run where we intercept the WriteAsync call.
        // Here we simply verify executor was called (i.e., the flow ran).
        executorMock.Verify(
            expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_FfmetaContentHasCorrectFormat()
    {
        string? capturedFfmetaContent = null;

        Mock<IFfmpegExecutor> executorMock = new();
        executorMock
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(
                action: (cmd, _, _, _, _) =>
                {
                    // Read the ffmeta file before executor returns (it gets deleted after)
                    string ffmetaPath = Path.Combine(path1: _tempDir, path2: ".chapters.ffmeta");
                    if (File.Exists(path: ffmetaPath))
                        capturedFfmetaContent = File.ReadAllText(path: ffmetaPath);
                }
            )
            .ReturnsAsync(value: new ExecutionResult(Success: true, ExitCode: 0, StdErr: string.Empty, Duration: TimeSpan.Zero, Error: null));

        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal(), ffmpegExecutor: executorMock.Object);
        await strategy.FinalizeAsync(outputDirectory: _tempDir, plan: CreatePlanWithChapters(), mediaTitle: "Movie", ct: default);

        capturedFfmetaContent.Should().NotBeNull();
        capturedFfmetaContent!.Should().StartWith(expected: ";FFMETADATA1");
        capturedFfmetaContent.Should().Contain(expected: "[CHAPTER]");
        capturedFfmetaContent.Should().Contain(expected: "TIMEBASE=1/1000");
        capturedFfmetaContent.Should().Contain(expected: "START=0");
        capturedFfmetaContent.Should().Contain(expected: "title=Opening");
        capturedFfmetaContent.Should().Contain(expected: "title=Act One");
        capturedFfmetaContent.Should().Contain(expected: "title=Finale");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_SecondFfmpegCallHasCorrectArgs()
    {
        FfmpegCommand? capturedCommand = null;

        Mock<IFfmpegExecutor> executorMock = new();
        executorMock
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(action: (cmd, _, _, _, _) => capturedCommand = cmd)
            .ReturnsAsync(value: new ExecutionResult(Success: true, ExitCode: 0, StdErr: string.Empty, Duration: TimeSpan.Zero, Error: null));

        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal(), ffmpegExecutor: executorMock.Object);
        await strategy.FinalizeAsync(outputDirectory: _tempDir, plan: CreatePlanWithChapters(), mediaTitle: "Movie", ct: default);

        capturedCommand.Should().NotBeNull();
        string args = string.Join(separator: " ", value: capturedCommand!.Arguments);
        args.Should().Contain(expected: "-map_metadata 1");
        args.Should().Contain(expected: "-metadata_header_padding 1024");
        args.Should().Contain(expected: "-c copy");
    }

    [Fact]
    public async Task FinalizeAsync_WithoutChapters_ExecutorNotCalled()
    {
        Mock<IFfmpegExecutor> executorMock = BuildExecutorMock();
        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal(), ffmpegExecutor: executorMock.Object);

        await strategy.FinalizeAsync(outputDirectory: _tempDir, plan: CreatePlanWithoutChapters(), mediaTitle: "Movie", ct: default);

        executorMock.Verify(
            expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_ChapterStartTimesAreInMilliseconds()
    {
        string? capturedFfmetaContent = null;

        Mock<IFfmpegExecutor> executorMock = new();
        executorMock
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(
                action: (cmd, _, _, _, _) =>
                {
                    string ffmetaPath = Path.Combine(path1: _tempDir, path2: ".chapters.ffmeta");
                    if (File.Exists(path: ffmetaPath))
                        capturedFfmetaContent = File.ReadAllText(path: ffmetaPath);
                }
            )
            .ReturnsAsync(value: new ExecutionResult(Success: true, ExitCode: 0, StdErr: string.Empty, Duration: TimeSpan.Zero, Error: null));

        Mp4OutputStrategy strategy = new(storage: TestStorageFactory.CreateLocal(), ffmpegExecutor: executorMock.Object);
        await strategy.FinalizeAsync(outputDirectory: _tempDir, plan: CreatePlanWithChapters(), mediaTitle: "Movie", ct: default);

        // Chapter 2 starts at 10 minutes = 600000 ms
        capturedFfmetaContent!.Should().Contain(expected: "START=600000");
        // Chapter 3 starts at 50 minutes = 3000000 ms
        capturedFfmetaContent.Should().Contain(expected: "START=3000000");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Mock<IFfmpegExecutor> BuildExecutorMock()
    {
        Mock<IFfmpegExecutor> mock = new();
        mock.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ExecutionResult(Success: true, ExitCode: 0, StdErr: string.Empty, Duration: TimeSpan.Zero, Error: null));
        return mock;
    }

    private static OutputPlan CreatePlanWithChapters() =>
        new(
            Format: OutputFormat.Mp4,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null,
            Chapters: ThreeChapters
        );

    private static OutputPlan CreatePlanWithoutChapters() =>
        new(
            Format: OutputFormat.Mp4,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [new(EncoderName: "aac", BitrateKbps: 192, Channels: 2, SampleRate: 48000, Action: StreamAction.Transcode, Language: "eng", MapLabel: "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
}
