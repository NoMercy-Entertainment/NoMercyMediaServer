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
    private readonly FontExtractor _extractor = new(TestStorageFactory.CreateLocal());
    private readonly string _tempDir;

    public FontExtractorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FontExtractorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static readonly IReadOnlyList<AttachmentInfo> TwoFonts =
    [
        new(
            5,
            "ttf",
            "ChalkDust_0.ttf",
            "application/x-truetype-font"
        ),
        new(6, "ttf", "Arial.ttf", "application/x-truetype-font"),
    ];

    // ------------------------------------------------------------------
    // BuildExtractionCommand emits one -dump_attachment flag per attachment
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_EmitsDumpFlagPerAttachment()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir,
            TwoFonts
        );

        cmd.Arguments.Should().Contain("-dump_attachment:5");
        cmd.Arguments.Should().Contain("-dump_attachment:6");
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
            new(6, "ttf", "CM Big Fat Paintbrush_0.ttf", null),
        ];

        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir,
            attachments
        );

        cmd.Arguments.Should().Contain("CM_Big_Fat_Paintbrush_0.ttf");
        cmd.Arguments.Should().NotContain(arg => arg.Contains(' '));
    }

    // ------------------------------------------------------------------
    // Two attachments that sanitize to the same name stay distinct
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_NameCollision_IsDisambiguated()
    {
        IReadOnlyList<AttachmentInfo> attachments =
        [
            new(5, "ttf", "My Font.ttf", null),
            new(6, "ttf", "My@Font.ttf", null),
        ];

        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir,
            attachments
        );

        List<string> names = cmd
            .Arguments.Where(a => a.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            .ToList();
        names.Should().OnlyHaveUniqueItems();
        names.Should().HaveCount(2);
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand sets working directory to fonts subfolder
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_WorkingDirectoryIsFontsSubdir()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir,
            TwoFonts
        );

        string expectedFontDir = Path.Combine(_tempDir, "fonts");
        cmd.WorkingDirectory.Should().Be(expectedFontDir);
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand uses configured ffmpeg path
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_UsesConfiguredFfmpegPath()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "/usr/bin/ffmpeg",
            "/input/movie.mkv",
            _tempDir,
            TwoFonts
        );

        cmd.Executable.Should().Be("/usr/bin/ffmpeg");
    }

    // ------------------------------------------------------------------
    // BuildExtractionCommand references input path
    // ------------------------------------------------------------------

    [Fact]
    public void BuildExtractionCommand_ContainsInputPath()
    {
        FfmpegCommand cmd = _extractor.BuildExtractionCommand(
            "ffmpeg",
            "/input/movie.mkv",
            _tempDir,
            TwoFonts
        );

        cmd.Arguments.Should().Contain("/input/movie.mkv");
    }

    // ------------------------------------------------------------------
    // CountFontAttachments counts fonts (incl. .ttc) and ignores non-fonts
    // ------------------------------------------------------------------

    [Fact]
    public void CountFontAttachments_CountsFontsIgnoresOthers()
    {
        IReadOnlyList<AttachmentInfo> attachments =
        [
            new(5, "ttf", "A.ttf", null),
            new(6, "otf", "B.otf", null),
            new(7, "ttf", "C.ttc", null),
            new(8, "bin", "grade.cube", null),
            new(9, "bin", null, null),
        ];

        _extractor.CountFontAttachments(attachments).Should().Be(3);
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: font files produce correct JSON
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_WithFontFiles_WritesCorrectJson()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Font.ttf"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Another.otf"), "dummy");

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        string manifestPath = Path.Combine(_tempDir, "fonts.json");
        File.Exists(manifestPath).Should().BeTrue();

        string json = await File.ReadAllTextAsync(manifestPath);
        JArray entries = JArray.Parse(json);
        entries.Should().HaveCount(2);

        List<string> files = entries.Select(e => e["file"]!.Value<string>()!).ToList();
        files.Should().Contain("fonts/Font.ttf");
        files.Should().Contain("fonts/Another.otf");

        List<string> mimeTypes = entries.Select(e => e["mime_type"]!.Value<string>()!).ToList();
        mimeTypes.Should().Contain("application/x-font-truetype");
        mimeTypes.Should().Contain("application/x-font-opentype");
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: WOFF and WOFF2 mime types
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_WoffFonts_CorrectMimeTypes()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Font.woff"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Font2.woff2"), "dummy");

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        string json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "fonts.json"));
        JArray entries = JArray.Parse(json);

        List<string> mimeTypes = entries.Select(e => e["mime_type"]!.Value<string>()!).ToList();
        mimeTypes.Should().Contain("font/woff");
        mimeTypes.Should().Contain("font/woff2");
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: empty fonts dir gets deleted, no manifest
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_EmptyFontsDir_DeletesDirAndNoManifest()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        Directory.Exists(fontDir).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "fonts.json")).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // WriteFontManifest: no fonts dir at all — no error, no manifest
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_NoFontsDir_DoesNotThrow()
    {
        // fonts/ subdirectory never created
        Func<Task> act = async () => await _extractor.WriteFontManifestAsync(_tempDir, default);

        await act.Should().NotThrowAsync();
        File.Exists(Path.Combine(_tempDir, "fonts.json")).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Unknown extension is excluded from fonts.json
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_UnknownExtension_ExcludedFromFontsJson()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);
        await File.WriteAllTextAsync(Path.Combine(fontDir, "font.ttf"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(fontDir, "profile.icc"), "dummy");

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        string json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "fonts.json"));
        JArray entries = JArray.Parse(json);
        entries.Should().HaveCount(1);
        entries[0]["file"]!.Value<string>().Should().Be("fonts/font.ttf");
    }

    // ------------------------------------------------------------------
    // .cube LUT is moved from fonts/ to luts/ — not in fonts.json
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_CubeLut_MovedToLutsDir()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);
        await File.WriteAllTextAsync(Path.Combine(fontDir, "YouTube_HDRtoSDR_1.cube"), "LUT data");

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        File.Exists(Path.Combine(_tempDir, "luts", "YouTube_HDRtoSDR_1.cube")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "fonts", "YouTube_HDRtoSDR_1.cube")).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "fonts.json")).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // .cube LUT produces a luts.json manifest with correct shape
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_CubeLut_WritesLutsJson()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);
        await File.WriteAllTextAsync(Path.Combine(fontDir, "YouTube_HDRtoSDR_1.cube"), "LUT data");

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        string lutsJsonPath = Path.Combine(_tempDir, "luts.json");
        File.Exists(lutsJsonPath).Should().BeTrue();

        string json = await File.ReadAllTextAsync(lutsJsonPath);
        JArray entries = JArray.Parse(json);
        entries.Should().HaveCount(1);
        entries[0]["file"]!.Value<string>().Should().Be("luts/YouTube_HDRtoSDR_1.cube");
        entries[0]["mime_type"]!.Value<string>().Should().Be("application/octet-stream");
    }

    // ------------------------------------------------------------------
    // Mix of font + LUT: font stays in fonts.json, LUT goes to luts.json
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteFontManifestAsync_MixedContent_SeparatedCorrectly()
    {
        string fontDir = Path.Combine(_tempDir, "fonts");
        Directory.CreateDirectory(fontDir);
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Arial.ttf"), "font data");
        await File.WriteAllTextAsync(Path.Combine(fontDir, "Grading.cube"), "LUT data");

        await _extractor.WriteFontManifestAsync(_tempDir, default);

        string fontsJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "fonts.json"));
        JArray fontEntries = JArray.Parse(fontsJson);
        fontEntries.Should().HaveCount(1);
        fontEntries[0]["file"]!.Value<string>().Should().Be("fonts/Arial.ttf");

        string lutsJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "luts.json"));
        JArray lutEntries = JArray.Parse(lutsJson);
        lutEntries.Should().HaveCount(1);
        lutEntries[0]["file"]!.Value<string>().Should().Be("luts/Grading.cube");
    }
}
