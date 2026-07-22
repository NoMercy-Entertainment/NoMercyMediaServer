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

using Newtonsoft.Json.Linq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.PostProcess;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.PostProcess;

public class FontExtractorTests : IDisposable
{
    private readonly FontExtractor _extractor = new(storage: TestStorageFactory.CreateLocal());
    private readonly string _tempDir;

    public FontExtractorTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"FontExtractorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    private static readonly IReadOnlyList<AttachmentInfo> TwoFonts =
    [
        new(
            Index: 5,
            Codec: "ttf",
            Filename: "ChalkDust_0.ttf",
            MimeType: "application/x-truetype-font"
        ),
        new(Index: 6, Codec: "ttf", Filename: "Arial.ttf", MimeType: "application/x-truetype-font"),
    ];

    // ------------------------------------------------------------------
    // BuildExtractionCommand emits one -dump_attachment flag per attachment
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_EmitsDumpFlagPerAttachment()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            ffmpegPath: "ffmpeg",
            inputPath: "/input/movie.mkv",
            outputDirectory: _tempDir,
            attachments: TwoFonts
        );

        cmd.Arguments.Should().Contain(expected: "-dump_attachment:5");
        cmd.Arguments.Should().Contain(expected: "-dump_attachment:6");
    }

    // ------------------------------------------------------------------
    // An attachment filename ffmpeg rejects as "unsafe" (spaces) is
    // sanitized to an explicit output name — the whole dump no longer aborts.
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_UnsafeFilename_IsSanitized()
    {
        IReadOnlyList<AttachmentInfo> attachments =
        [
            new(Index: 6, Codec: "ttf", Filename: "CM Big Fat Paintbrush_0.ttf", MimeType: null),
        ];

        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            ffmpegPath: "ffmpeg",
            inputPath: "/input/movie.mkv",
            outputDirectory: _tempDir,
            attachments: attachments
        );

        cmd.Arguments.Should().Contain(expected: "CM_Big_Fat_Paintbrush_0.ttf");
        cmd.Arguments.Should().NotContain(predicate: arg => arg.Contains(' '));
    }

    // ------------------------------------------------------------------
    // Two attachments that sanitize to the same name stay distinct
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_NameCollision_IsDisambiguated()
    {
        IReadOnlyList<AttachmentInfo> attachments =
        [
            new(Index: 5, Codec: "ttf", Filename: "My Font.ttf", MimeType: null),
            new(Index: 6, Codec: "ttf", Filename: "My@Font.ttf", MimeType: null),
        ];

        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            ffmpegPath: "ffmpeg",
            inputPath: "/input/movie.mkv",
            outputDirectory: _tempDir,
            attachments: attachments
        );

        List<string> names = cmd
            .Arguments.Where(predicate: a => a.EndsWith(value: ".ttf", comparisonType: StringComparison.OrdinalIgnoreCase))
            .ToList();
        names.Should().OnlyHaveUniqueItems();
        names.Should().HaveCount(expected: 2);
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand sets working directory to fonts subfolder
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_WorkingDirectoryIsFontsSubdir()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            ffmpegPath: "ffmpeg",
            inputPath: "/input/movie.mkv",
            outputDirectory: _tempDir,
            attachments: TwoFonts
        );

        string expectedFontDir = Path.Combine(path1: _tempDir, path2: "fonts");
        cmd.WorkingDirectory.Should().Be(expected: expectedFontDir);
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand uses configured ffmpeg path
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_UsesConfiguredFfmpegPath()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            ffmpegPath: "/usr/bin/ffmpeg",
            inputPath: "/input/movie.mkv",
            outputDirectory: _tempDir,
            attachments: TwoFonts
        );

        cmd.Executable.Should().Be(expected: "/usr/bin/ffmpeg");
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand references input path
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_ContainsInputPath()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            ffmpegPath: "ffmpeg",
            inputPath: "/input/movie.mkv",
            outputDirectory: _tempDir,
            attachments: TwoFonts
        );

        cmd.Arguments.Should().Contain(expected: "/input/movie.mkv");
    }

    // ------------------------------------------------------------------
    // CountFontAttachments counts fonts (incl. .ttc) and ignores non-fonts
    // ------------------------------------------------------------------

    [Fact]
    public void CountFontAttachments_CountsFontsIgnoresOthers()
    {
        IReadOnlyList<AttachmentInfo> attachments =
        [
            new(Index: 5, Codec: "ttf", Filename: "A.ttf", MimeType: null),
            new(Index: 6, Codec: "otf", Filename: "B.otf", MimeType: null),
            new(Index: 7, Codec: "ttf", Filename: "C.ttc", MimeType: null),
            new(Index: 8, Codec: "bin", Filename: "grade.cube", MimeType: null),
            new(Index: 9, Codec: "bin", Filename: null, MimeType: null),
        ];

        _extractor.CountFontAttachments(attachments: attachments).Should().Be(expected: 3);
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: font files produce correct JSON
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_WithFontFiles_WritesCorrectJson()
    {
        string fontDir = Path.Combine(path1: _tempDir, path2: "fonts");
        Directory.CreateDirectory(path: fontDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "Font.ttf"), contents: "dummy");
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "Another.otf"), contents: "dummy");

        await _extractor.WriteFontManifestAsync(outputDirectory: _tempDir, ct: default);

        string manifestPath = Path.Combine(path1: _tempDir, path2: "fonts.json");
        File.Exists(path: manifestPath).Should().BeTrue();

        string json = await File.ReadAllTextAsync(path: manifestPath);
        JArray entries = JArray.Parse(json: json);
        entries.Should().HaveCount(expected: 2);

        List<string> files = entries.Select(selector: e => e[key: "file"]!.Value<string>()!).ToList();
        files.Should().Contain(expected: "fonts/Font.ttf");
        files.Should().Contain(expected: "fonts/Another.otf");

        List<string> mimeTypes = entries.Select(selector: e => e[key: "mime_type"]!.Value<string>()!).ToList();
        mimeTypes.Should().Contain(expected: "application/x-font-truetype");
        mimeTypes.Should().Contain(expected: "application/x-font-opentype");
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: WOFF and WOFF2 mime types
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_WoffFonts_CorrectMimeTypes()
    {
        string fontDir = Path.Combine(path1: _tempDir, path2: "fonts");
        Directory.CreateDirectory(path: fontDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "Font.woff"), contents: "dummy");
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "Font2.woff2"), contents: "dummy");

        await _extractor.WriteFontManifestAsync(outputDirectory: _tempDir, ct: default);

        string json = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "fonts.json"));
        JArray entries = JArray.Parse(json: json);

        List<string> mimeTypes = entries.Select(selector: e => e[key: "mime_type"]!.Value<string>()!).ToList();
        mimeTypes.Should().Contain(expected: "font/woff");
        mimeTypes.Should().Contain(expected: "font/woff2");
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: empty fonts dir gets deleted, no manifest
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_EmptyFontsDir_DeletesDirAndNoManifest()
    {
        string fontDir = Path.Combine(path1: _tempDir, path2: "fonts");
        Directory.CreateDirectory(path: fontDir);

        await _extractor.WriteFontManifestAsync(outputDirectory: _tempDir, ct: default);

        Directory.Exists(path: fontDir).Should().BeFalse();
        File.Exists(path: Path.Combine(path1: _tempDir, path2: "fonts.json")).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: no fonts dir at all — no error, no manifest
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_NoFontsDir_DoesNotThrow()
    {
        // fonts/ subdirectory never created
        Func<Task> act = async () => await _extractor.WriteFontManifestAsync(outputDirectory: _tempDir, ct: default);

        await act.Should().NotThrowAsync();
        File.Exists(path: Path.Combine(path1: _tempDir, path2: "fonts.json")).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Unknown extension is excluded from fonts.json
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_UnknownExtension_ExcludedFromFontsJson()
    {
        string fontDir = Path.Combine(path1: _tempDir, path2: "fonts");
        Directory.CreateDirectory(path: fontDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "font.ttf"), contents: "dummy");
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "profile.icc"), contents: "dummy");

        await _extractor.WriteFontManifestAsync(outputDirectory: _tempDir, ct: default);

        string json = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "fonts.json"));
        JArray entries = JArray.Parse(json: json);
        entries.Should().HaveCount(expected: 1);
        entries[index: 0][key: "file"]!.Value<string>().Should().Be(expected: "fonts/font.ttf");
    }

    // ------------------------------------------------------------------
    // .cube LUT is moved from fonts/ to luts/ — not in fonts.json
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_CubeLut_MovedToLutsDir()
    {
        string fontDir = Path.Combine(path1: _tempDir, path2: "fonts");
        Directory.CreateDirectory(path: fontDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "YouTube_HDRtoSDR_1.cube"), contents: "LUT data");

        await _extractor.WriteFontManifestAsync(outputDirectory: _tempDir, ct: default);

        File.Exists(path: Path.Combine(path1: _tempDir, path2: "luts", path3: "YouTube_HDRtoSDR_1.cube")).Should().BeTrue();
        File.Exists(path: Path.Combine(path1: _tempDir, path2: "fonts", path3: "YouTube_HDRtoSDR_1.cube")).Should().BeFalse();
        File.Exists(path: Path.Combine(path1: _tempDir, path2: "fonts.json")).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // .cube LUT produces a luts.json manifest with correct shape
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_CubeLut_WritesLutsJson()
    {
        string fontDir = Path.Combine(path1: _tempDir, path2: "fonts");
        Directory.CreateDirectory(path: fontDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "YouTube_HDRtoSDR_1.cube"), contents: "LUT data");

        await _extractor.WriteFontManifestAsync(outputDirectory: _tempDir, ct: default);

        string lutsJsonPath = Path.Combine(path1: _tempDir, path2: "luts.json");
        File.Exists(path: lutsJsonPath).Should().BeTrue();

        string json = await File.ReadAllTextAsync(path: lutsJsonPath);
        JArray entries = JArray.Parse(json: json);
        entries.Should().HaveCount(expected: 1);
        entries[index: 0][key: "file"]!.Value<string>().Should().Be(expected: "luts/YouTube_HDRtoSDR_1.cube");
        entries[index: 0][key: "mime_type"]!.Value<string>().Should().Be(expected: "application/octet-stream");
    }

    // ------------------------------------------------------------------
    // Mix of font + LUT: font stays in fonts.json, LUT goes to luts.json
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_MixedContent_SeparatedCorrectly()
    {
        string fontDir = Path.Combine(path1: _tempDir, path2: "fonts");
        Directory.CreateDirectory(path: fontDir);
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "Arial.ttf"), contents: "font data");
        await File.WriteAllTextAsync(path: Path.Combine(path1: fontDir, path2: "Grading.cube"), contents: "LUT data");

        await _extractor.WriteFontManifestAsync(outputDirectory: _tempDir, ct: default);

        string fontsJson = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "fonts.json"));
        JArray fontEntries = JArray.Parse(json: fontsJson);
        fontEntries.Should().HaveCount(expected: 1);
        fontEntries[index: 0][key: "file"]!.Value<string>().Should().Be(expected: "fonts/Arial.ttf");

        string lutsJson = await File.ReadAllTextAsync(path: Path.Combine(path1: _tempDir, path2: "luts.json"));
        JArray lutEntries = JArray.Parse(json: lutsJson);
        lutEntries.Should().HaveCount(expected: 1);
        lutEntries[index: 0][key: "file"]!.Value<string>().Should().Be(expected: "luts/Grading.cube");
    }
}
