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
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.Tests.MediaProcessing.Files;

// ---------------------------------------------------------------------------
// Real on-disk fixtures for the FileManager.Hashing asset scanners that back
// a rescan's Metadata: subtitles (text vs. bitmap sidecar rejection),
// preview sprite/thumbnail pairing, fonts, and the VTT dimension/image-size
// helpers. Every case builds a real temp directory tree and drives the
// private scanner methods through LocalStorage exactly like a live scan
// would — no mock stands in for the unit under test.
// ---------------------------------------------------------------------------
[Trait(name: "Category", value: "Unit")]
public sealed class FileManagerAssetHashListTests : IDisposable
{
    private readonly string _tempRoot;

    public FileManagerAssetHashListTests()
    {
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-assets-{Guid.NewGuid():N}");
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

    private static object InvokePrivate(FileManager manager, string methodName, object?[] args)
    {
        MethodInfo method =
            typeof(FileManager).GetMethod(
                name: methodName,
                bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
            ) ?? throw new InvalidOperationException(message: $"{methodName} not found");
        return method.Invoke(obj: manager, parameters: args)!;
    }

    private static List<ISubtitle> InvokeGetSubtitleHashList(
        FileManager manager,
        IStorage storage,
        string hostFolder
    ) => (List<ISubtitle>)InvokePrivate(manager: manager, methodName: "GetSubtitleHashList", args: [storage, hostFolder]);

    private static List<IFont> InvokeGetFontHashList(
        FileManager manager,
        IStorage storage,
        string hostFolder
    ) => (List<IFont>)InvokePrivate(manager: manager, methodName: "GetFontHashList", args: [storage, hostFolder]);

    private static List<IPreview> InvokeGetPreviewHashList(
        FileManager manager,
        IStorage storage,
        string hostFolder,
        List<VideoTrack> extraFiles
    ) =>
        (List<IPreview>)
            InvokePrivate(manager: manager, methodName: "GetPreviewHashList", args: [storage, hostFolder, extraFiles]);

    private static List<VideoTrack> InvokeGetExtraFiles(IStorage storage, string hostFolder) =>
        (List<VideoTrack>)
            typeof(FileManager)
                .GetMethod(name: "GetExtraFiles", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(obj: null, parameters: [storage, hostFolder])!;

    private static List<Subtitle> InvokeGetSubtitles(IStorage storage, string hostFolder) =>
        (List<Subtitle>)
            typeof(FileManager)
                .GetMethod(name: "GetSubtitles", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(obj: null, parameters: [storage, hostFolder])!;

    private static (int Width, int Height) InvokeGetImageDimensions(string filePath)
    {
        object result = typeof(FileManager)
            .GetMethod(name: "GetImageDimensions", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(obj: null, parameters: [filePath])!;
        return ((int, int))result;
    }

    private static (int Width, int Height) InvokeGetImageDimensionsFromVtt(
        IStorage storage,
        string filePath
    )
    {
        object result = typeof(FileManager)
            .GetMethod(name: "GetImageDimensionsFromVtt", bindingAttr: BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(obj: null, parameters: [storage, filePath])!;
        return ((int, int))result;
    }

    // -----------------------------------------------------------------------
    // GetSubtitleHashList — the Metadata-facing subtitle scanner.
    // -----------------------------------------------------------------------

    [Fact]
    public void GetSubtitleHashList_NoSubtitlesFolder_ReturnsEmpty()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.NoSubs");
        Directory.CreateDirectory(path: hostDir);

        List<ISubtitle> result = InvokeGetSubtitleHashList(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetSubtitleHashList_TextSubtitle_IsIncludedWithLanguageTypeAndCodec()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.WithSubs");
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllText(path: Path.Combine(path1: subtitleDir, path2: "Movie.eng.full.vtt"), contents: "WEBVTT\n");

        List<ISubtitle> result = InvokeGetSubtitleHashList(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir
        );

        result.Should().ContainSingle();
        result[index: 0].Language.Should().Be(expected: "eng");
        result[index: 0].Type.Should().Be(expected: "full");
        result[index: 0].Codec.Should().Be(expected: "vtt");
        result[index: 0].FileName.Should().Be(expected: "/subtitles/Movie.eng.full.vtt");
    }

    [Theory]
    [InlineData(data: "sup")]
    [InlineData(data: "idx")]
    [InlineData(data: "vob")]
    [InlineData(data: "mks")]
    public void GetSubtitleHashList_BitmapSidecarExtensions_AreExcluded(string bitmapExtension)
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: $"Movie.Bitmap.{bitmapExtension}");
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllBytes(path: Path.Combine(path1: subtitleDir, path2: $"Movie.eng.full.{bitmapExtension}"), bytes: [0x00]);

        List<ISubtitle> result = InvokeGetSubtitleHashList(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir
        );

        result.Should().BeEmpty(because: "bitmap sidecars cannot be streamed as HLS text sidecars");
    }

    [Theory]
    [InlineData(data: "alt")]
    [InlineData(data: "sdh")]
    [InlineData(data: "forced")]
    [InlineData(data: "sign")]
    [InlineData(data: "song")]
    [InlineData(data: "commentary")]
    [InlineData(data: "director")]
    public void GetSubtitleHashList_AnyTypeToken_Matches(string type)
    {
        // Regression: the matcher used to only accept type in
        // (sign, song, full) — every "alt"/"sdh"/"forced" subtitle silently
        // vanished from the track list.
        string hostDir = Path.Combine(path1: _tempRoot, path2: $"Movie.Type.{type}");
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllText(path: Path.Combine(path1: subtitleDir, path2: $"Movie.eng.{type}.srt"), contents: "1\n");

        List<ISubtitle> result = InvokeGetSubtitleHashList(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir
        );

        result.Should().ContainSingle();
        result[index: 0].Type.Should().Be(expected: type);
    }

    [Fact]
    public void GetSubtitleHashList_TwoCharLanguageCode_Matches()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.TwoCharLang");
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllText(path: Path.Combine(path1: subtitleDir, path2: "Movie.en.full.vtt"), contents: "WEBVTT\n");

        List<ISubtitle> result = InvokeGetSubtitleHashList(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir
        );

        result.Should().ContainSingle();
        result[index: 0].Language.Should().Be(expected: "en");
    }

    // -----------------------------------------------------------------------
    // GetFontHashList
    // -----------------------------------------------------------------------

    [Fact]
    public void GetFontHashList_NoFontsFolder_ReturnsEmpty()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.NoFonts");
        Directory.CreateDirectory(path: hostDir);

        List<IFont> result = InvokeGetFontHashList(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetFontHashList_FontsPresent_ReturnsEachWithHashAndSize()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.WithFonts");
        string fontsDir = Path.Combine(path1: hostDir, path2: "fonts");
        Directory.CreateDirectory(path: fontsDir);
        File.WriteAllBytes(path: Path.Combine(path1: fontsDir, path2: "Arial.ttf"), bytes: new byte[128]);
        File.WriteAllBytes(path: Path.Combine(path1: fontsDir, path2: "Comic.otf"), bytes: new byte[64]);

        List<IFont> result = InvokeGetFontHashList(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir
        );

        result.Should().HaveCount(expected: 2);
        result.Should().Contain(predicate: f => f.FileName == "/fonts/Arial.ttf" && f.FileSize == 128);
        result.Should().Contain(predicate: f => f.FileName == "/fonts/Comic.otf" && f.FileSize == 64);
        result.Should().OnlyContain(predicate: f => !string.IsNullOrEmpty(f.FileHash));
    }

    // -----------------------------------------------------------------------
    // GetImageDimensions / GetImageDimensionsFromVtt
    // -----------------------------------------------------------------------

    [Fact]
    public void GetImageDimensions_RealPngFile_ReturnsItsActualSize()
    {
        string filePath = Path.Combine(path1: _tempRoot, path2: "sprite.png");
        WritePng(path: filePath, width: 64, height: 32);

        (int width, int height) = InvokeGetImageDimensions(filePath: filePath);

        width.Should().Be(expected: 64);
        height.Should().Be(expected: 32);
    }

    [Fact]
    public void GetImageDimensionsFromVtt_ParsesXywhFromVttContents()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.Thumbs");
        Directory.CreateDirectory(path: hostDir);
        string vttPath = Path.Combine(path1: hostDir, path2: "thumbs.vtt");
        File.WriteAllText(
            path: vttPath,
            contents: "WEBVTT\n\n" + "00:00:00.000 --> 00:00:05.000\n" + "sprite.jpg#xywh=0,0,320,180\n"
        );

        (int width, int height) = InvokeGetImageDimensionsFromVtt(storage: BuildLocalStorage(), filePath: vttPath);

        width.Should().Be(expected: 320);
        height.Should().Be(expected: 180);
    }

    [Fact]
    public void GetImageDimensionsFromVtt_NoXywhToken_ReturnsZeroZero()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.NoXywh");
        Directory.CreateDirectory(path: hostDir);
        string vttPath = Path.Combine(path1: hostDir, path2: "thumbs.vtt");
        File.WriteAllText(path: vttPath, contents: "WEBVTT\n\n00:00:00.000 --> 00:00:05.000\nsprite.jpg\n");

        (int width, int height) = InvokeGetImageDimensionsFromVtt(storage: BuildLocalStorage(), filePath: vttPath);

        width.Should().Be(expected: 0);
        height.Should().Be(expected: 0);
    }

    // -----------------------------------------------------------------------
    // GetPreviewHashList — pairs a sprite (Kind="sprite") with its matching
    // thumbnails VTT (Kind="thumbnails") by position (the sprite/thumbnails
    // extraFiles lists are produced index-aligned by GetExtraFiles).
    // -----------------------------------------------------------------------

    [Fact]
    public void GetPreviewHashList_SpriteAndThumbnailPair_ProducesOnePreviewWithDimensions()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.Previews");
        Directory.CreateDirectory(path: hostDir);
        WritePng(path: Path.Combine(path1: hostDir, path2: "sprite_320x180.webp"), width: 320, height: 180);
        File.WriteAllText(
            path: Path.Combine(path1: hostDir, path2: "thumbs_320x180.vtt"),
            contents: "WEBVTT\n\n00:00:00.000 --> 00:00:05.000\nsprite_320x180.webp#xywh=0,0,320,180\n"
        );

        List<VideoTrack> extraFiles =
        [
            new() { File = "/sprite_320x180.webp", Kind = "sprite" },
            new() { File = "/thumbs_320x180.vtt", Kind = "thumbnails" },
        ];

        List<IPreview> result = InvokeGetPreviewHashList(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir,
            extraFiles: extraFiles
        );

        result.Should().ContainSingle();
        result[index: 0].ImageFileName.Should().Be(expected: "/sprite_320x180.webp");
        result[index: 0].TimeFileName.Should().Be(expected: "/thumbs_320x180.vtt");
        result[index: 0].Width.Should().Be(expected: 320);
        result[index: 0].Height.Should().Be(expected: 180);
        result[index: 0].ImageFileSize.Should().BeGreaterThan(expected: 0);
        result[index: 0].TimeFileSize.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public void GetPreviewHashList_NoSpriteOrThumbnailEntries_ReturnsEmpty()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.NoPreviews");
        Directory.CreateDirectory(path: hostDir);

        List<IPreview> result = InvokeGetPreviewHashList(
            manager: BuildFileManager(),
            storage: BuildLocalStorage(),
            hostFolder: hostDir,
            extraFiles: []
        );

        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // GetExtraFiles — chapter/skipper/sprite/thumbnail/font classification,
    // and the stale-VTT-without-matching-sprite exclusion.
    // -----------------------------------------------------------------------

    [Fact]
    public void GetExtraFiles_ClassifiesChapterSkipperSpriteThumbnailAndFonts()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.Extras");
        Directory.CreateDirectory(path: hostDir);
        File.WriteAllText(path: Path.Combine(path1: hostDir, path2: "chapters.vtt"), contents: "WEBVTT\n");
        File.WriteAllText(path: Path.Combine(path1: hostDir, path2: "skipper.json"), contents: "{}");
        File.WriteAllBytes(path: Path.Combine(path1: hostDir, path2: "sprite_320x180.webp"), bytes: new byte[16]);
        // Same stem as the webp ("sprite_320x180") — GetExtraFiles only
        // registers a thumbnails VTT when it has a matching sprite webp.
        File.WriteAllText(path: Path.Combine(path1: hostDir, path2: "sprite_320x180.vtt"), contents: "WEBVTT\n");
        File.WriteAllText(path: Path.Combine(path1: hostDir, path2: "fonts.tar"), contents: "x");

        List<VideoTrack> tracks = InvokeGetExtraFiles(storage: BuildLocalStorage(), hostFolder: hostDir);

        tracks.Should().Contain(predicate: t => t.Kind == "chapters" && t.File == "/chapters.vtt");
        tracks.Should().Contain(predicate: t => t.Kind == "skippers" && t.File == "/skipper.json");
        tracks.Should().Contain(predicate: t => t.Kind == "sprite" && t.File == "/sprite_320x180.webp");
        tracks.Should().Contain(predicate: t => t.Kind == "thumbnails" && t.File == "/sprite_320x180.vtt");
        tracks.Should().Contain(predicate: t => t.Kind == "fonts" && t.File == "/fonts.tar");
    }

    [Fact]
    public void GetExtraFiles_StaleVttWithoutMatchingSpriteStem_IsExcluded()
    {
        // Regression: a re-encode at a different sprite dimension leaves the
        // old VTT behind (thumbs_320x178.vtt) alongside the live sprite
        // (thumbs_320x180.webp). The stale VTT must not be registered — the
        // player would follow its cues to a 404.
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.StaleVtt");
        Directory.CreateDirectory(path: hostDir);
        File.WriteAllBytes(path: Path.Combine(path1: hostDir, path2: "thumbs_320x180.webp"), bytes: new byte[16]);
        File.WriteAllText(path: Path.Combine(path1: hostDir, path2: "thumbs_320x178.vtt"), contents: "WEBVTT\n");

        List<VideoTrack> tracks = InvokeGetExtraFiles(storage: BuildLocalStorage(), hostFolder: hostDir);

        tracks.Should().Contain(predicate: t => t.Kind == "sprite");
        tracks.Should().NotContain(predicate: t => t.Kind == "thumbnails");
    }

    // -----------------------------------------------------------------------
    // GetSubtitles — the DB-facing (lightweight, no hash) subtitle list, and
    // its orphaned-bitmap detection.
    // -----------------------------------------------------------------------

    [Fact]
    public void GetSubtitles_NoSubtitlesFolder_ReturnsEmpty()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.NoSubsDb");
        Directory.CreateDirectory(path: hostDir);

        List<Subtitle> result = InvokeGetSubtitles(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetSubtitles_TextSubtitle_IsIncluded()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.SubsDb");
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllText(path: Path.Combine(path1: subtitleDir, path2: "Movie.eng.full.ass"), contents: "[Script Info]\n");

        List<Subtitle> result = InvokeGetSubtitles(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().ContainSingle();
        result[index: 0].Language.Should().Be(expected: "eng");
        result[index: 0].Type.Should().Be(expected: "full");
        result[index: 0].Ext.Should().Be(expected: "ass");
    }

    [Fact]
    public void GetSubtitles_BitmapWithoutSiblingVtt_IsExcludedFromResult()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.OrphanBitmap");
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllBytes(path: Path.Combine(path1: subtitleDir, path2: "Movie.jpn.full.sup"), bytes: [0x00]);

        List<Subtitle> result = InvokeGetSubtitles(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().BeEmpty(because: "a bitmap sidecar is never itself a playable text track");
    }

    [Fact]
    public void GetSubtitles_BitmapWithSiblingVtt_SiblingIsIncludedBitmapIsNot()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.OcrPaired");
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllBytes(path: Path.Combine(path1: subtitleDir, path2: "Movie.jpn.full.sup"), bytes: [0x00]);
        File.WriteAllText(path: Path.Combine(path1: subtitleDir, path2: "Movie.jpn.full.vtt"), contents: "WEBVTT\n");

        List<Subtitle> result = InvokeGetSubtitles(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().ContainSingle();
        result[index: 0].Ext.Should().Be(expected: "vtt");
    }

    [Fact]
    public void GetSubtitles_UnrecognizedFileNameAlongsideRealSubtitle_IsSkippedNotThrown()
    {
        string hostDir = Path.Combine(path1: _tempRoot, path2: "Movie.SubsDbWithJunk");
        string subtitleDir = Path.Combine(path1: hostDir, path2: "subtitles");
        Directory.CreateDirectory(path: subtitleDir);
        File.WriteAllText(path: Path.Combine(path1: subtitleDir, path2: "README.txt"), contents: "not a subtitle");
        File.WriteAllText(path: Path.Combine(path1: subtitleDir, path2: "Movie.eng.full.ass"), contents: "[Script Info]\n");

        List<Subtitle> result = InvokeGetSubtitles(storage: BuildLocalStorage(), hostFolder: hostDir);

        result.Should().ContainSingle();
        result[index: 0].Language.Should().Be(expected: "eng");
    }

    private static void WritePng(string path, int width, int height)
    {
        using Image<Rgba32> image = new(width: width, height: height);
        image.SaveAsPng(path: path);
    }
}
