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
        _tempDir = Path.Combine(Path.GetTempPath(), $"Mp4ChapterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Write a dummy "output.mp4" so the rename in FinalizeAsync succeeds
        File.WriteAllText(Path.Combine(_tempDir, "output.mp4"), "dummy");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static readonly IReadOnlyList<ChapterInfo> ThreeChapters =
    [
        new(TimeSpan.Zero, TimeSpan.FromMinutes(10), "Opening"),
        new(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(50), "Act One"),
        new(TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(90), "Finale"),
    ];

    // ------------------------------------------------------------------
    // ffmeta file content
    // ------------------------------------------------------------------

    [Fact]
    public async Task FinalizeAsync_WithChapters_WritesFfmetaFile()
    {
        Mock<IFfmpegExecutor> executorMock = BuildExecutorMock();
        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal(), executorMock.Object);

        await strategy.FinalizeAsync(_tempDir, CreatePlanWithChapters(), "Movie", default);

        // ffmeta is deleted after re-mux; but the executor was called — verified below.
        // The content test uses a second run where we intercept the WriteAsync call.
        // Here we simply verify executor was called (i.e., the flow ran).
        executorMock.Verify(
            e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_FfmetaContentHasCorrectFormat()
    {
        string? capturedFfmetaContent = null;

        Mock<IFfmpegExecutor> executorMock = new();
        executorMock
            .Setup(e =>
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
                (cmd, _, _, _, _) =>
                {
                    // Read the ffmeta file before executor returns (it gets deleted after)
                    string ffmetaPath = Path.Combine(_tempDir, ".chapters.ffmeta");
                    if (File.Exists(ffmetaPath))
                        capturedFfmetaContent = File.ReadAllText(ffmetaPath);
                }
            )
            .ReturnsAsync(new ExecutionResult(true, 0, string.Empty, TimeSpan.Zero, null));

        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal(), executorMock.Object);
        await strategy.FinalizeAsync(_tempDir, CreatePlanWithChapters(), "Movie", default);

        capturedFfmetaContent.Should().NotBeNull();
        capturedFfmetaContent!.Should().StartWith(";FFMETADATA1");
        capturedFfmetaContent.Should().Contain("[CHAPTER]");
        capturedFfmetaContent.Should().Contain("TIMEBASE=1/1000");
        capturedFfmetaContent.Should().Contain("START=0");
        capturedFfmetaContent.Should().Contain("title=Opening");
        capturedFfmetaContent.Should().Contain("title=Act One");
        capturedFfmetaContent.Should().Contain("title=Finale");
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_SecondFfmpegCallHasCorrectArgs()
    {
        FfmpegCommand? capturedCommand = null;

        Mock<IFfmpegExecutor> executorMock = new();
        executorMock
            .Setup(e =>
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
            >((cmd, _, _, _, _) => capturedCommand = cmd)
            .ReturnsAsync(new ExecutionResult(true, 0, string.Empty, TimeSpan.Zero, null));

        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal(), executorMock.Object);
        await strategy.FinalizeAsync(_tempDir, CreatePlanWithChapters(), "Movie", default);

        capturedCommand.Should().NotBeNull();
        string args = string.Join(" ", capturedCommand!.Arguments);
        args.Should().Contain("-map_metadata 1");
        args.Should().Contain("-metadata_header_padding 1024");
        args.Should().Contain("-c copy");
    }

    [Fact]
    public async Task FinalizeAsync_WithoutChapters_ExecutorNotCalled()
    {
        Mock<IFfmpegExecutor> executorMock = BuildExecutorMock();
        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal(), executorMock.Object);

        await strategy.FinalizeAsync(_tempDir, CreatePlanWithoutChapters(), "Movie", default);

        executorMock.Verify(
            e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task FinalizeAsync_WithChapters_ChapterStartTimesAreInMilliseconds()
    {
        string? capturedFfmetaContent = null;

        Mock<IFfmpegExecutor> executorMock = new();
        executorMock
            .Setup(e =>
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
                (cmd, _, _, _, _) =>
                {
                    string ffmetaPath = Path.Combine(_tempDir, ".chapters.ffmeta");
                    if (File.Exists(ffmetaPath))
                        capturedFfmetaContent = File.ReadAllText(ffmetaPath);
                }
            )
            .ReturnsAsync(new ExecutionResult(true, 0, string.Empty, TimeSpan.Zero, null));

        Mp4OutputStrategy strategy = new(TestStorageFactory.CreateLocal(), executorMock.Object);
        await strategy.FinalizeAsync(_tempDir, CreatePlanWithChapters(), "Movie", default);

        // Chapter 2 starts at 10 minutes = 600000 ms
        capturedFfmetaContent!.Should().Contain("START=600000");
        // Chapter 3 starts at 50 minutes = 3000000 ms
        capturedFfmetaContent.Should().Contain("START=3000000");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Mock<IFfmpegExecutor> BuildExecutorMock()
    {
        Mock<IFfmpegExecutor> mock = new();
        mock.Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ExecutionResult(true, 0, string.Empty, TimeSpan.Zero, null));
        return mock;
    }

    private static OutputPlan CreatePlanWithChapters() =>
        new(
            Format: OutputFormat.Mp4,
            VideoOutputs:
            [
                new(
                    1920,
                    1080,
                    "libx264",
                    23,
                    0,
                    "medium",
                    "high",
                    "4.0",
                    false,
                    "yuv420p",
                    "[v0]",
                    new()
                ),
            ],
            AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")],
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
                    1920,
                    1080,
                    "libx264",
                    23,
                    0,
                    "medium",
                    "high",
                    "4.0",
                    false,
                    "yuv420p",
                    "[v0]",
                    new()
                ),
            ],
            AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "0:a:0")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
}
