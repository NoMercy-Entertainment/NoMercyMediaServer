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
// Remaining branch gaps in GetVideoHashList / GetAudioHashList /
// GetSubtitleHashList / ParseChaptersVtt / ComputeFileHash that the earlier
// test files (FileManagerAssetHashListTests, FileManagerHashListTests,
// FileManagerMasterRebuildTests) don't exercise: the "rendition dir exists
// but is empty/malformed" paths a live scan hits whenever an encode is
// interrupted mid-write, and ComputeFileHash's stat-failure fallback.
// ---------------------------------------------------------------------------
[Trait(name: "Category", value: "Unit")]
public sealed class FileManagerHashingBranchGapTests : IDisposable
{
    private readonly string _tempRoot;

    public FileManagerHashingBranchGapTests()
    {
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-branchgap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempRoot))
            Directory.Delete(path: _tempRoot, recursive: true);
    }

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

    private static object InvokePrivate(string methodName, object?[] args, bool isStatic = false)
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                name: methodName,
                bindingAttr: (isStatic ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.NonPublic
            ) ?? throw new InvalidOperationException(message: $"{methodName} not found");
        return method.Invoke(obj: isStatic ? null : BuildFileManager(), parameters: args)!;
    }

    private static List<IVideo> InvokeGetVideoHashList(IStorage storage, string hostFolder) =>
        (List<IVideo>)
            typeof(FileManager)
                .GetMethod(name: "GetVideoHashList", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(obj: BuildFileManager(), parameters: [storage, hostFolder])!;

    private static List<IAudio> InvokeGetAudioHashList(IStorage storage, string hostFolder) =>
        (List<IAudio>)
            typeof(FileManager)
                .GetMethod(name: "GetAudioHashList", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(obj: BuildFileManager(), parameters: [storage, hostFolder])!;

    private static List<ISubtitle> InvokeGetSubtitleHashList(IStorage storage, string hostFolder) =>
        (List<ISubtitle>)
            typeof(FileManager)
                .GetMethod(name: "GetSubtitleHashList", bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(obj: BuildFileManager(), parameters: [storage, hostFolder])!;

    // -----------------------------------------------------------------------
    // GetVideoHashList
    // -----------------------------------------------------------------------

    [Fact]
    public void GetVideoHashList_HostFolderMissing_ReturnsEmpty()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "does-not-exist");

        List<IVideo> result = InvokeGetVideoHashList(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetVideoHashList_VideoDirNameDoesNotMatchWidthHeightPattern_IsSkipped()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.BadVideoDir");
        Directory.CreateDirectory(path: Path.Combine(path1: hostDir, path2: "video_bogus"));
        File.WriteAllText(path: Path.Combine(path1: hostDir, path2: "video_bogus", path3: "video_bogus.m3u8"), contents: "#EXTM3U");

        List<IVideo> result = InvokeGetVideoHashList(storage: BuildLocalStorage(), hostFolder: hostDir);

        result
            .Should()
            .BeEmpty(because: "a video_* dir name that doesn't parse as WxH must be skipped, not throw");
    }

    [Fact]
    public void GetVideoHashList_MatchingDirWithNestedSubdirButNoPlaylist_IsSkipped()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.NoPlaylist");
        string videoDir = Path.Combine(path1: hostDir, path2: "video_1920x1080");
        Directory.CreateDirectory(path: videoDir);
        // A nested subdirectory forces the playlist lookup's `!e.IsDirectory`
        // guard to see a real directory entry, not just files.
        Directory.CreateDirectory(path: Path.Combine(path1: videoDir, path2: "stray-subdir"));
        File.WriteAllBytes(path: Path.Combine(path1: videoDir, path2: "segment0.ts"), bytes: new byte[8]);
        // No .m3u8 file at all — playlist lookup must come back null.

        List<IVideo> result = InvokeGetVideoHashList(storage: BuildLocalStorage(), hostFolder: hostDir);

        result
            .Should()
            .BeEmpty(because: "a rendition dir with no playlist (interrupted encode) must be skipped");
    }

    [Fact]
    public void GetVideoHashList_MatchingDirWithNestedSubdirAndPlaylist_IsIncluded()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.WithSubdirAndPlaylist");
        string videoDir = Path.Combine(path1: hostDir, path2: "video_1920x1080");
        Directory.CreateDirectory(path: videoDir);
        Directory.CreateDirectory(path: Path.Combine(path1: videoDir, path2: "stray-subdir"));
        File.WriteAllText(path: Path.Combine(path1: videoDir, path2: "video_1920x1080.m3u8"), contents: "#EXTM3U");
        File.WriteAllBytes(path: Path.Combine(path1: videoDir, path2: "segment0.ts"), bytes: new byte[8]);

        List<IVideo> result = InvokeGetVideoHashList(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().ContainSingle();
        result[index: 0].Width.Should().Be(expected: 1920);
        result[index: 0].Height.Should().Be(expected: 1080);
    }

    // -----------------------------------------------------------------------
    // GetAudioHashList
    // -----------------------------------------------------------------------

    [Fact]
    public void GetAudioHashList_HostFolderMissing_ReturnsEmpty()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "does-not-exist-audio");

        List<IAudio> result = InvokeGetAudioHashList(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAudioHashList_AudioDirNameDoesNotMatchLanguagePattern_IsSkipped()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.BadAudioDir");
        // "toolong" is more than the 2-3 letter language token the regex allows.
        Directory.CreateDirectory(path: Path.Combine(path1: hostDir, path2: "audio_toolonglanguage"));
        File.WriteAllText(path: Path.Combine(path1: hostDir, path2: "audio_toolonglanguage", path3: "x.m3u8"), contents: "#EXTM3U");

        List<IAudio> result = InvokeGetAudioHashList(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAudioHashList_MatchingDirWithNestedSubdirButNoPlaylist_IsSkipped()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.AudioNoPlaylist");
        string audioDir = Path.Combine(path1: hostDir, path2: "audio_eng_aac");
        Directory.CreateDirectory(path: audioDir);
        Directory.CreateDirectory(path: Path.Combine(path1: audioDir, path2: "stray-subdir"));
        File.WriteAllBytes(path: Path.Combine(path1: audioDir, path2: "segment0.ts"), bytes: new byte[8]);

        List<IAudio> result = InvokeGetAudioHashList(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().BeEmpty(because: "an audio rendition dir with no playlist must be skipped");
    }

    [Fact]
    public void GetAudioHashList_OldNamingNoCodecSuffix_DefaultsToAac()
    {
        // Direct GetAudioHashList-level regression pin for the old-naming
        // (no `_<codec>` token) audio dir — the master-rebuild path already
        // pins this end to end; this covers the codec ternary's false branch
        // in GetAudioHashList itself.
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Show.OldNamingAudio");
        string audioDir = Path.Combine(path1: hostDir, path2: "audio_jpn");
        Directory.CreateDirectory(path: audioDir);
        File.WriteAllText(path: Path.Combine(path1: audioDir, path2: "audio_jpn.m3u8"), contents: "#EXTM3U");

        List<IAudio> result = InvokeGetAudioHashList(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().ContainSingle();
        result[index: 0].Language.Should().Be(expected: "jpn");
        result[index: 0].Codec.Should().Be(expected: "aac");
    }

    // -----------------------------------------------------------------------
    // GetSubtitleHashList — a file in subtitles/ that doesn't match the
    // {lang}.{type}.{ext} shape at all (not a bitmap rejection — no match).
    // -----------------------------------------------------------------------

    [Fact]
    public void GetSubtitleHashList_UnrecognizedFileName_IsSkipped()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.JunkInSubs");
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllText(path: Path.Combine(path1: subtitleDir, path2: "README.txt"), contents: "not a subtitle");

        List<ISubtitle> result = InvokeGetSubtitleHashList(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // ParseChaptersVtt: a cue whose block is JUST the timing line (no
    // preceding id line, no following title line) — the title ternary's
    // false branch.
    // -----------------------------------------------------------------------

    [Fact]
    public void ParseChaptersVtt_TimingLineWithNoTitleLine_ProducesEmptyTitle()
    {
        // Starts at 0 so NormalizeChapters has nothing to prepend — isolates
        // the title ternary's false branch (no line after the timing line).
        const string text = "WEBVTT\n\n00:00:00.000 --> 00:00:10.000\n";

        List<IChapter> chapters = FileManager.ParseChaptersVtt(text: text);

        chapters.Should().ContainSingle();
        chapters[index: 0].Title.Should().BeEmpty();
        chapters[index: 0].StartTime.Should().Be(expected: 0);
        chapters[index: 0].EndTime.Should().Be(expected: 10000);
    }

    // -----------------------------------------------------------------------
    // ComputeFileHash: storage.LastModified throwing must not blow up the
    // hash — falls back to modifiedTicks = 0, still hashing size+ticks.
    // -----------------------------------------------------------------------

    [Fact]
    public void ComputeFileHash_LastModifiedThrows_FallsBackToZeroTicksInsteadOfThrowing()
    {
        Mock<IStorage> storage = new();
        storage.Setup(expression: s => s.SizeOrZero("some/path")).Returns(value: 42);
        storage.Setup(expression: s => s.LastModified("some/path")).Throws(exception: new IOException(message: "stat failed"));

        MethodInfo method =
            typeof(FileManager).GetMethod(
                name: "ComputeFileHash",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Static
            ) ?? throw new InvalidOperationException(message: "ComputeFileHash not found");

        string hash = (string)method.Invoke(obj: null, parameters: [storage.Object, "some/path"])!;

        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(expected: 64, because: "SHA-256 hex digest");
    }
}
