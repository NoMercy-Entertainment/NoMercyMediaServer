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

using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.PostProcess;

public class ThumbnailGeneratorTests : IDisposable
{
    private readonly ThumbnailGenerator _gen = new(storage: TestStorageFactory.CreateLocal());
    private readonly string _tempDir;

    public ThumbnailGeneratorTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"ThumbnailGeneratorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    // ------------------------------------------------------------------
    // BuildCaptureCommand has correct fps filter
    // ------------------------------------------------------------------

    [Fact]
    public void BuildCaptureCommand_HasCorrectFpsFilter()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildCaptureCommand(
            ffmpegPath: "ffmpeg",
            inputPath: "/input/movie.mkv",
            outputDirectory: _tempDir,
            plan: plan,
            duration: TimeSpan.FromMinutes(minutes: 90)
        );

        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "fps=1/10");
        args.Should().Contain(expected: "scale=320:-2");
    }

    // ------------------------------------------------------------------
    // BuildCaptureCommand maps first video stream
    // ------------------------------------------------------------------

    [Fact]
    public void BuildCaptureCommand_MapsFirstVideoStream()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildCaptureCommand(
            ffmpegPath: "ffmpeg",
            inputPath: "/input/movie.mkv",
            outputDirectory: _tempDir,
            plan: plan,
            duration: TimeSpan.FromMinutes(minutes: 90)
        );

        cmd.Arguments.Should().Contain(expected: "0:v:0");
    }

    // ------------------------------------------------------------------
    // BuildCaptureCommand output path includes thumbs directory and pattern
    // ------------------------------------------------------------------

    [Fact]
    public void BuildCaptureCommand_OutputPathIsInsideThumbsDir()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildCaptureCommand(
            ffmpegPath: "ffmpeg",
            inputPath: "/input/movie.mkv",
            outputDirectory: _tempDir,
            plan: plan,
            duration: TimeSpan.FromMinutes(minutes: 90)
        );

        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "thumbs_320");
        args.Should().Contain(expected: "thumb_%04d.jpg");
    }

    // ------------------------------------------------------------------
    // BuildSpriteCommand has correct tile dimensions for 9 images
    // 9 images → 3x3 grid
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSpriteCommand_NineImages_ThreeByThreeGrid()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildSpriteCommand(ffmpegPath: "ffmpeg", outputDirectory: _tempDir, plan: plan, imageCount: 9);

        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "tile=3x3");
    }

    // ------------------------------------------------------------------
    // BuildSpriteCommand 4 images → 2x2 grid
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSpriteCommand_FourImages_TwoByTwoGrid()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildSpriteCommand(ffmpegPath: "ffmpeg", outputDirectory: _tempDir, plan: plan, imageCount: 4);

        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "tile=2x2");
    }

    // ------------------------------------------------------------------
    // BuildSpriteCommand 10 images → 4x3 grid (ceil(sqrt(10))=4, ceil(10/4)=3)
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSpriteCommand_TenImages_FourByThreeGrid()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildSpriteCommand(ffmpegPath: "ffmpeg", outputDirectory: _tempDir, plan: plan, imageCount: 10);

        string args = string.Join(separator: " ", value: cmd.Arguments);
        args.Should().Contain(expected: "tile=4x3");
    }

    // ------------------------------------------------------------------
    // BuildSpriteCommand output is a webp file
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSpriteCommand_OutputIsWebp()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        FfmpegCommand cmd = _gen.BuildSpriteCommand(ffmpegPath: "ffmpeg", outputDirectory: _tempDir, plan: plan, imageCount: 9);

        cmd.Arguments.Last().Should().EndWith(expected: ".webp");
    }

    // ------------------------------------------------------------------
    // WriteVttCueFileAsync produces valid WEBVTT header
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteVttCueFileAsync_WritesWebvttHeader()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        await _gen.WriteVttCueFileAsync(
            outputDirectory: _tempDir,
            plan: plan,
            imageCount: 3,
            duration: TimeSpan.FromSeconds(seconds: 30),
            ct: default
        );

        string vttFile = Path.Combine(path1: _tempDir, path2: "thumbs_320x218.vtt");
        File.Exists(path: vttFile).Should().BeTrue();

        string content = await File.ReadAllTextAsync(path: vttFile);
        content.Should().StartWith(expected: "WEBVTT");
    }

    // ------------------------------------------------------------------
    // WriteVttCueFileAsync correct timestamps
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteVttCueFileAsync_CorrectTimestamps()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        await _gen.WriteVttCueFileAsync(
            outputDirectory: _tempDir,
            plan: plan,
            imageCount: 3,
            duration: TimeSpan.FromSeconds(seconds: 30),
            ct: default
        );

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "thumbs_320x218.vtt"));

        // First cue: 00:00:00.000 --> 00:00:10.000
        content.Should().Contain(expected: "00:00:00.000 --> 00:00:10.000");
        // Second cue: 00:00:10.000 --> 00:00:20.000
        content.Should().Contain(expected: "00:00:10.000 --> 00:00:20.000");
        // Third cue: 00:00:20.000 --> 00:00:30.000
        content.Should().Contain(expected: "00:00:20.000 --> 00:00:30.000");
    }

    // ------------------------------------------------------------------
    // WriteVttCueFileAsync correct xywh coordinates
    // First image is at col=0, row=0 → x=0, y=0
    // Second image (3-col grid) is at col=1, row=0 → x=320, y=0
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteVttCueFileAsync_CorrectXywhCoordinates()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        // 9 images → 3x3 grid, thumbHeight = 320*9/16 = 180
        await _gen.WriteVttCueFileAsync(
            outputDirectory: _tempDir,
            plan: plan,
            imageCount: 9,
            duration: TimeSpan.FromSeconds(seconds: 90),
            ct: default
        );

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "thumbs_320x218.vtt"));

        // First frame: col=0, row=0 → xywh=0,0,320,218
        content.Should().Contain(expected: "thumbs_320x218.webp#xywh=0,0,320,218");
        // Second frame: col=1, row=0 → xywh=320,0,320,218
        content.Should().Contain(expected: "thumbs_320x218.webp#xywh=320,0,320,218");
        // Fourth frame: col=0, row=1 → xywh=0,218,320,218
        content.Should().Contain(expected: "thumbs_320x218.webp#xywh=0,218,320,218");
    }

    // ------------------------------------------------------------------
    // WriteVttCueFileAsync last cue is clamped to duration
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteVttCueFileAsync_LastCueClamped()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        // imageCount=3, duration=25s → last cue end = min(30, 25) = 25s
        await _gen.WriteVttCueFileAsync(
            outputDirectory: _tempDir,
            plan: plan,
            imageCount: 3,
            duration: TimeSpan.FromSeconds(seconds: 25),
            ct: default
        );

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "thumbs_320x218.vtt"));
        content.Should().Contain(expected: "00:00:20.000 --> 00:00:25.000");
    }

    // ------------------------------------------------------------------
    // CleanupIndividualThumbnails removes the thumbs directory
    // ------------------------------------------------------------------

    [Fact]
    public void CleanupIndividualThumbnails_RemovesThumbsDirectory()
    {
        ThumbnailOutputPlan plan = new(Width: 320, Height: 218, IntervalSeconds: 10);
        string thumbDir = Path.Combine(path1: _tempDir, path2: "thumbs_320");
        Directory.CreateDirectory(path: thumbDir);
        File.WriteAllText(path: Path.Combine(path1: thumbDir, path2: "thumb_0001.jpg"), contents: "dummy");

        _gen.CleanupIndividualThumbnails(outputDirectory: _tempDir, plan: plan);

        Directory.Exists(path: thumbDir).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // CleanupIndividualThumbnails is safe when directory does not exist
    // ------------------------------------------------------------------

    [Fact]
    public void CleanupIndividualThumbnails_MissingDir_DoesNotThrow()
    {
        ThumbnailOutputPlan plan = new(Width: 999, Height: 562, IntervalSeconds: 10);
        Action act = () => _gen.CleanupIndividualThumbnails(outputDirectory: _tempDir, plan: plan);
        act.Should().NotThrow();
    }

    // ------------------------------------------------------------------
    // ComputeGrid: various image counts
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(data: [1, 1, 1])]
    [InlineData(data: [4, 2, 2])]
    [InlineData(data: [9, 3, 3])]
    [InlineData(data: [10, 4, 3])]
    [InlineData(data: [16, 4, 4])]
    [InlineData(data: [17, 5, 4])]
    public void ComputeGrid_CorrectDimensions(int imageCount, int expectedW, int expectedH)
    {
        (int w, int h) = ThumbnailGenerator.ComputeGrid(imageCount: imageCount);

        w.Should().Be(expected: expectedW);
        h.Should().Be(expected: expectedH);
    }
}
