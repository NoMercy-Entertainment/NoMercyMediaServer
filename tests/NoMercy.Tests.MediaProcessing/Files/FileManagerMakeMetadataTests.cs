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
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.MediaProcessing.Files;
using NoMercy.NmSystem.Dto;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.MediaProcessing.Files;

// ---------------------------------------------------------------------------
// MakeMetadata is the method that assembles every hash-list scanner in
// FileManager.Hashing.cs (video/audio/subtitle/font/preview/chapter) into the
// single Metadata record a rescan persists. This drives it end to end
// against a real fixture tree with a PRE-WRITTEN master.m3u8 that already
// advertises every on-disk rendition, so MasterCoversDiskOutput's skip guard
// fires and RebuildHlsMasterFromDiskAsync never needs the real ffprobe
// binary (no IMediaAnalyzer call — verified via the mock's Invocations).
// ---------------------------------------------------------------------------
[Trait(name: "Category", value: "Unit")]
public sealed class FileManagerMakeMetadataTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly Mock<IMediaAnalyzer> _mediaAnalyzer = new();

    public FileManagerMakeMetadataTests()
    {
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-makemeta-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempRoot))
            Directory.Delete(path: _tempRoot, recursive: true);
    }

    private FileManager BuildFileManager()
    {
        Mock<IFileRepository> repoMock = new();
        Mock<IStorageFactory> factoryMock = new();
        Mock<IStorageDriver> driverMock = new();
        return new(fileRepository: repoMock.Object, storageFactory: factoryMock.Object, storageDriver: driverMock.Object, mediaAnalyzer: _mediaAnalyzer.Object);
    }

    private static IStorage BuildLocalStorage()
    {
        LocalStorageDriver driver = new();
        return new LocalStorage(driver: driver, guard: new StoragePathGuard(allowedRoots: [], driver: driver));
    }

    private static async Task<Metadata> InvokeMakeMetadata(
        FileManager manager,
        IStorage storage,
        MediaFile item,
        string fileName,
        string baseFolder,
        string hostFolder,
        List<VideoTrack> extraFiles
    )
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                name: "MakeMetadata",
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException(message: "MakeMetadata not found");

        return await (Task<Metadata>)
            method.Invoke(obj: manager, parameters: [storage, item, fileName, baseFolder, hostFolder, extraFiles])!;
    }

    [Fact]
    public async Task MakeMetadata_FullFixtureTree_AssemblesEveryAssetKindWithoutReprobing()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "The.Full.Fixture.(2025).NoMercy");
        Directory.CreateDirectory(path: hostDir);

        // Video rendition.
        string videoDir = Path.Combine(path1: hostDir, path2: "video_1920x1080_SDR");
        Directory.CreateDirectory(path: videoDir);
        File.WriteAllText(path: Path.Combine(path1: videoDir, path2: "video_1920x1080_SDR.m3u8"), contents: "#EXTM3U");
        File.WriteAllBytes(path: Path.Combine(path1: videoDir, path2: "segment0.ts"), bytes: new byte[1000]);

        // Audio rendition.
        string audioDir = Path.Combine(path1: hostDir, path2: "audio_eng_aac");
        Directory.CreateDirectory(path: audioDir);
        File.WriteAllText(path: Path.Combine(path1: audioDir, path2: "audio_eng_aac.m3u8"), contents: "#EXTM3U");
        File.WriteAllBytes(path: Path.Combine(path1: audioDir, path2: "segment0.ts"), bytes: new byte[500]);

        // Subtitle.
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllText(path: Path.Combine(path1: subtitleDir, path2: "Fixture.eng.full.vtt"), contents: "WEBVTT\n");

        // Font.
        string fontsDir = Path.Combine(path1: hostDir, path2: "fonts");
        Directory.CreateDirectory(path: fontsDir);
        File.WriteAllBytes(path: Path.Combine(path1: fontsDir, path2: "Arial.ttf"), bytes: new byte[64]);

        // Chapters.
        File.WriteAllText(
            path: Path.Combine(path1: hostDir, path2: "chapters.vtt"),
            contents: "WEBVTT\n\nChapter 1\n00:00:00.000 --> 00:01:00.000\nIntro\n"
        );

        // Preview sprite + thumbnails pair (matching stem so GetExtraFiles
        // would pair them too — MakeMetadata is given the extraFiles list
        // directly here, mirroring what GetExtraFiles produces).
        File.WriteAllBytes(path: Path.Combine(path1: hostDir, path2: "sprite_320x180.webp"), bytes: new byte[200]);
        File.WriteAllText(
            path: Path.Combine(path1: hostDir, path2: "sprite_320x180.vtt"),
            contents: "WEBVTT\n\n00:00:00.000 --> 00:00:05.000\nsprite_320x180.webp#xywh=0,0,320,180\n"
        );

        // Pre-written master that already advertises every rendition above —
        // MasterCoversDiskOutput must see this as complete and skip the
        // reprobe entirely (subtitles/ here is flat files, not language
        // subdirectories, so that half of the check trivially passes).
        string masterPath = Path.Combine(path1: hostDir, path2: "The.Full.Fixture.(2025).NoMercy.m3u8");
        File.WriteAllText(
            path: masterPath,
            contents: "#EXTM3U\n"
                      + "#EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080\n"
                      + "video_1920x1080_SDR/video_1920x1080_SDR.m3u8\n"
                      + "#EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID=\"audio_aac\",LANGUAGE=\"eng\"\n"
                      + "audio_eng_aac/audio_eng_aac.m3u8\n"
        );

        List<VideoTrack> extraFiles =
        [
            new() { File = "/chapters.vtt", Kind = "chapters" },
            new() { File = "/sprite_320x180.webp", Kind = "sprite" },
            new() { File = "/sprite_320x180.vtt", Kind = "thumbnails" },
            new() { File = "/fonts/Arial.ttf", Kind = "fonts" },
        ];

        MediaFile item = new() { Path = hostDir, FFprobe = null };

        FileManager manager = BuildFileManager();
        IStorage storage = BuildLocalStorage();

        Metadata metadata = await InvokeMakeMetadata(
            manager: manager,
            storage: storage,
            item: item,
            fileName: "/The.Full.Fixture.(2025).NoMercy.m3u8",
            baseFolder: "/The.Full.Fixture.(2025).NoMercy",
            hostFolder: hostDir,
            extraFiles: extraFiles
        );

        _mediaAnalyzer
            .Invocations.Should()
            .BeEmpty(because: "the master already covers every on-disk rendition");

        metadata.Video.Should().ContainSingle();
        metadata.Video![index: 0].Width.Should().Be(expected: 1920);

        metadata.Audio.Should().ContainSingle();
        metadata.Audio![index: 0].Language.Should().Be(expected: "eng");

        metadata.Subtitles.Should().ContainSingle();
        metadata.Subtitles![index: 0].Language.Should().Be(expected: "eng");

        metadata.Fonts.Should().ContainSingle();

        metadata.Previews.Should().ContainSingle();
        metadata.Previews![index: 0].Width.Should().Be(expected: 320);

        metadata.Chapters.Should().ContainSingle();
        metadata.Chapters![index: 0].Title.Should().Be(expected: "Intro");

        metadata.ChapterFile.Should().NotBeNull();
        metadata.ChapterFile!.FileName.Should().Be(expected: "/chapters.vtt");

        metadata.FontsFile.Should().NotBeNull();

        // FolderSize sums every catalogued asset's on-disk size — a sanity
        // floor, not an exact byte count (compressed hashes etc. vary).
        metadata.FolderSize.Should().BeGreaterThan(expected: 1000 + 500);

        metadata.Type.Should().Be(expected: NoMercy.Database.Models.Media.MediaType.Tv);
    }

    [Fact]
    public async Task MakeMetadata_NoChaptersOrFontsExtraFiles_LeavesThoseFieldsNull()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "No.Extras.(2025).NoMercy");
        Directory.CreateDirectory(path: hostDir);

        MediaFile item = new() { Path = hostDir, FFprobe = null };
        FileManager manager = BuildFileManager();
        IStorage storage = BuildLocalStorage();

        Metadata metadata = await InvokeMakeMetadata(
            manager: manager,
            storage: storage,
            item: item,
            fileName: "/No.Extras.(2025).NoMercy.m3u8",
            baseFolder: "/No.Extras.(2025).NoMercy",
            hostFolder: hostDir,
            extraFiles: []
        );

        metadata.ChapterFile.Should().BeNull();
        metadata.FontsFile.Should().BeNull();
        metadata.Video.Should().BeEmpty();
        metadata.FolderSize.Should().Be(expected: 0);
    }
}
