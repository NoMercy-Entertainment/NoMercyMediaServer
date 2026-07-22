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

using System.Reflection;
using Moq;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.MediaProcessing.Files;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.MediaProcessing.Files;

// ---------------------------------------------------------------------------
// FileManager.ParseVttTimestampMs is the private helper ParseChaptersVtt
// (already covered by ChapterVttParserTests.cs against real WEBVTT text)
// delegates to for every timing group. These tests drive it directly to
// exercise the hours/minutes/seconds TryParse failure branches a
// regex-constrained caller can never reach (ChapterTimingRegex only ever
// matches digit sequences), plus the NOTE/STYLE/REGION block-skip branches
// in ParseChaptersVtt itself, and GetChapterHashListAsync end-to-end against
// a real chapter file on disk.
// ---------------------------------------------------------------------------
[Trait(name: "Category", value: "Unit")]
public sealed class FileManagerChapterTimestampTests : IDisposable
{
    private readonly string _tempRoot;

    public FileManagerChapterTimestampTests()
    {
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-chapters-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempRoot))
            Directory.Delete(path: _tempRoot, recursive: true);
    }

    private static int InvokeParseVttTimestampMs(string timestamp)
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                name: "ParseVttTimestampMs",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Static
            ) ?? throw new InvalidOperationException(message: "ParseVttTimestampMs not found");
        return (int)method.Invoke(obj: null, parameters: [timestamp])!;
    }

    // -----------------------------------------------------------------------
    // 3-component (HH:MM:SS.mmm) — valid, and each TryParse failure branch.
    // -----------------------------------------------------------------------

    [Fact]
    public void ParseVttTimestampMs_ThreePart_Valid_ReturnsWholeMilliseconds()
    {
        InvokeParseVttTimestampMs(timestamp: "01:02:03.500").Should().Be(expected: 3723500);
    }

    [Fact]
    public void ParseVttTimestampMs_ThreePart_HoursUnparseable_ReturnsMinusOne()
    {
        InvokeParseVttTimestampMs(timestamp: "ab:02:03.500").Should().Be(expected: -1);
    }

    [Fact]
    public void ParseVttTimestampMs_ThreePart_MinutesUnparseable_ReturnsMinusOne()
    {
        InvokeParseVttTimestampMs(timestamp: "01:ab:03.500").Should().Be(expected: -1);
    }

    [Fact]
    public void ParseVttTimestampMs_ThreePart_SecondsUnparseable_ReturnsMinusOne()
    {
        InvokeParseVttTimestampMs(timestamp: "01:02:ab").Should().Be(expected: -1);
    }

    // -----------------------------------------------------------------------
    // 2-component (MM:SS.mmm) — valid, and each TryParse failure branch.
    // -----------------------------------------------------------------------

    [Fact]
    public void ParseVttTimestampMs_TwoPart_Valid_ReturnsWholeMilliseconds()
    {
        InvokeParseVttTimestampMs(timestamp: "02:03.500").Should().Be(expected: 123500);
    }

    [Fact]
    public void ParseVttTimestampMs_TwoPart_MinutesUnparseable_ReturnsMinusOne()
    {
        InvokeParseVttTimestampMs(timestamp: "ab:03.500").Should().Be(expected: -1);
    }

    [Fact]
    public void ParseVttTimestampMs_TwoPart_SecondsUnparseable_ReturnsMinusOne()
    {
        InvokeParseVttTimestampMs(timestamp: "02:ab").Should().Be(expected: -1);
    }

    // -----------------------------------------------------------------------
    // Neither 2 nor 3 colon-separated parts — the `else return -1` branch.
    // Unreachable from ParseChaptersVtt's real call path (ChapterTimingRegex
    // only ever captures a 2- or 3-part digit group), so only directly
    // testable by calling the helper itself.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(data: "01:02:03:04")]
    [InlineData(data: "05")]
    [InlineData(data: "")]
    public void ParseVttTimestampMs_WrongPartCount_ReturnsMinusOne(string timestamp)
    {
        InvokeParseVttTimestampMs(timestamp: timestamp).Should().Be(expected: -1);
    }

    // -----------------------------------------------------------------------
    // ParseChaptersVtt: NOTE / STYLE / REGION blocks are metadata, never
    // cues — must be skipped, not mis-parsed as a garbage chapter.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(data: "NOTE this is a comment")]
    [InlineData(data: "STYLE\n::cue { color: white; }")]
    [InlineData(data: "REGION\nid:bottom\nwidth:40%")]
    public void ParseChaptersVtt_MetadataBlock_IsSkipped_NotEmittedAsChapter(string metadataBlock)
    {
        string text =
            "WEBVTT\n\n" + metadataBlock + "\n\n" + "00:00:10.000 --> 00:00:20.000\nReal Chapter\n";

        List<IChapter> chapters = FileManager.ParseChaptersVtt(text: text);

        chapters.Should().ContainSingle(predicate: c => c.Title == "Real Chapter");
    }

    // -----------------------------------------------------------------------
    // GetChapterHashListAsync — the async wrapper that reads a real chapter
    // file off disk and maps ParseChaptersVtt's output onto IChapter DTOs.
    // -----------------------------------------------------------------------

    private static FileManager BuildFileManager()
    {
        Mock<IFileRepository> repoMock = new();
        Mock<IStorageFactory> factoryMock = new();
        Mock<IStorageDriver> driverMock = new();
        Mock<IMediaAnalyzer> mediaAnalyzerMock = new();
        return new(
            fileRepository: repoMock.Object,
            storageFactory: factoryMock.Object,
            storageDriver: driverMock.Object,
            mediaAnalyzer: mediaAnalyzerMock.Object
        );
    }

    private static IStorage BuildLocalStorage()
    {
        LocalStorageDriver driver = new();
        return new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
    }

    private static async Task<List<IChapter>> InvokeGetChapterHashListAsync(
        FileManager manager,
        IStorage storage,
        string hostFolder,
        string file
    )
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                name: "GetChapterHashListAsync",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException(message: "GetChapterHashListAsync not found");

        return await (Task<List<IChapter>>)method.Invoke(obj: manager, parameters: [storage, hostFolder, file])!;
    }

    [Fact]
    public async Task GetChapterHashListAsync_RealChapterFile_ReturnsParsedChapters()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.Chapters");
        Directory.CreateDirectory(path: hostDir);
        File.WriteAllText(
            path: Path.Combine(path1: hostDir, path2: "chapters.vtt"),
            contents: "WEBVTT\n\nChapter 1\n00:00:00.000 --> 00:01:00.000\nIntro\n"
        );

        List<IChapter> result = await InvokeGetChapterHashListAsync(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir,
            file: "chapters.vtt"
        );

        result.Should().ContainSingle();
        result[index: 0].Title.Should().Be(expected: "Intro");
        result[index: 0].StartTime.Should().Be(expected: 0);
        result[index: 0].EndTime.Should().Be(expected: 60000);
    }

    [Fact]
    public async Task GetChapterHashListAsync_EmptyChapterFile_ReturnsEmptyList()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.EmptyChapters");
        Directory.CreateDirectory(path: hostDir);
        File.WriteAllText(path: Path.Combine(path1: hostDir, path2: "chapters.vtt"), contents: "WEBVTT\n");

        List<IChapter> result = await InvokeGetChapterHashListAsync(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir,
            file: "chapters.vtt"
        );

        result.Should().BeEmpty();
    }
}
