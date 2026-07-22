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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.PostProcess;

public class ChapterWriterTests : IDisposable
{
    private readonly ChapterWriter _writer = new(storage: TestStorageFactory.CreateLocal());
    private readonly string _tempDir;

    public ChapterWriterTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"ChapterWriterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    // ------------------------------------------------------------------
    // Empty chapters list — no file created
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_EmptyChapters_NoFileCreated()
    {
        await _writer.WriteChaptersAsync(outputDirectory: _tempDir, chapters: [], ct: default);

        string chaptersFile = Path.Combine(path1: _tempDir, path2: "chapters.vtt");
        File.Exists(path: chaptersFile).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Valid chapters write a WEBVTT header
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_ValidChapters_WritesWebvttHeader()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 5), Title: "Opening"),
        ];

        await _writer.WriteChaptersAsync(outputDirectory: _tempDir, chapters: chapters, ct: default);

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "chapters.vtt"));
        content.Should().StartWith(expected: "WEBVTT");
    }

    // ------------------------------------------------------------------
    // Timestamps are formatted correctly
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_FormatsTimestampsCorrectly()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromSeconds(seconds: 90), Title: "Part 1"),
            new(Start: TimeSpan.FromSeconds(seconds: 90), End: TimeSpan.FromMinutes(minutes: 30), Title: "Part 2"),
        ];

        await _writer.WriteChaptersAsync(outputDirectory: _tempDir, chapters: chapters, ct: default);

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "chapters.vtt"));

        content.Should().Contain(expected: "00:00:00.000 --> 00:01:30.000");
        content.Should().Contain(expected: "00:01:30.000 --> 00:30:00.000");
    }

    // ------------------------------------------------------------------
    // Chapter titles appear in the file
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_TitlesAppearInFile()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 5), Title: "Opening Credits"),
            new(
                Start: TimeSpan.FromMinutes(minutes: 5),
                End: TimeSpan.FromMinutes(minutes: 60),
                Title: "Main Feature"
            ),
        ];

        await _writer.WriteChaptersAsync(outputDirectory: _tempDir, chapters: chapters, ct: default);

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "chapters.vtt"));

        content.Should().Contain(expected: "Opening Credits");
        content.Should().Contain(expected: "Main Feature");
    }

    // ------------------------------------------------------------------
    // Null title falls back to "Chapter N"
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_NullTitle_FallsBackToChapterN()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 5), Title: null),
        ];

        await _writer.WriteChaptersAsync(outputDirectory: _tempDir, chapters: chapters, ct: default);

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "chapters.vtt"));
        content.Should().Contain(expected: "Chapter 1");
    }

    // ------------------------------------------------------------------
    // Multiple chapters all appear with correct cue numbers
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_MultipleChapters_CorrectCueNumbers()
    {
        ChapterInfo[] chapters =
        [
            new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 10), Title: "One"),
            new(Start: TimeSpan.FromMinutes(minutes: 10), End: TimeSpan.FromMinutes(minutes: 20), Title: "Two"),
            new(Start: TimeSpan.FromMinutes(minutes: 20), End: TimeSpan.FromMinutes(minutes: 30), Title: "Three"),
        ];

        await _writer.WriteChaptersAsync(outputDirectory: _tempDir, chapters: chapters, ct: default);

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "chapters.vtt"));

        content.Should().Contain(expected: "Chapter 1");
        content.Should().Contain(expected: "Chapter 2");
        content.Should().Contain(expected: "Chapter 3");
    }

    // ------------------------------------------------------------------
    // Hours component formats correctly for long durations
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_LongDuration_HoursFormattedCorrectly()
    {
        ChapterInfo[] chapters =
        [
            new(
                Start: TimeSpan.FromHours(hours: 1) + TimeSpan.FromMinutes(minutes: 30),
                End: TimeSpan.FromHours(hours: 2),
                Title: "Act 3"
            ),
        ];

        await _writer.WriteChaptersAsync(outputDirectory: _tempDir, chapters: chapters, ct: default);

        string content = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "chapters.vtt"));
        content.Should().Contain(expected: "01:30:00.000 --> 02:00:00.000");
    }

    // ------------------------------------------------------------------
    // File is created at the expected path
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteChaptersAsync_CreatesFileAtCorrectPath()
    {
        ChapterInfo[] chapters = [new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 5), Title: "Intro")];

        await _writer.WriteChaptersAsync(outputDirectory: _tempDir, chapters: chapters, ct: default);

        string expectedPath = Path.Combine(path1: _tempDir, path2: "chapters.vtt");
        File.Exists(path: expectedPath).Should().BeTrue();
    }
}
