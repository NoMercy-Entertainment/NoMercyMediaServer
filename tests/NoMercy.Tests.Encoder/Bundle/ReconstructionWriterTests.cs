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

using Newtonsoft.Json;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Bundle;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

file static class ReconstructionTestData
{
    public static MediaInfo MinimalMediaInfo(string filePath = "/media/fight-club.mkv") =>
        new(
            FilePath: filePath,
            Format: "matroska",
            Duration: TimeSpan.FromSeconds(7800),
            OverallBitRateKbps: 25_000,
            FileSizeBytes: 24_375_000_000L,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "hevc",
                    Width: 1920,
                    Height: 800,
                    FrameRate: 23.976,
                    BitDepth: 10,
                    PixelFormat: "yuv420p10le",
                    ColorPrimaries: "bt2020",
                    ColorTransfer: "smpte2084",
                    ColorSpace: "bt2020nc",
                    IsDefault: true,
                    BitRateKbps: 20_000
                ),
            ],
            AudioStreams:
            [
                new(
                    Index: 1,
                    Codec: "truehd",
                    Channels: 8,
                    SampleRate: 48_000,
                    BitRateKbps: 4_000,
                    Language: "eng",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams:
            [
                new(Index: 2, Codec: "ass", Language: "eng", IsDefault: false, IsForced: false),
            ],
            Chapters: [],
            Attachments: []
        );

    public static BundleLayout HlsLayout() =>
        new(
            MediaKey: "mfa",
            PresetSlug: "web-1080p",
            IsSingleFile: false,
            BundleDirectory: "encodes/web-1080p",
            MasterPlaylistName: "mfa_master.m3u8",
            ManifestPath: "encodes/web-1080p/manifest.json",
            ReconstructionPath: "encodes/web-1080p/reconstruction.json",
            SingleFileName: string.Empty,
            PresetId: "01HZABC",
            PresetName: "Web 1080p",
            ContainerString: "hls-fmp4"
        );

    public static BundleLayout MkvLayout() =>
        new(
            MediaKey: "mfa",
            PresetSlug: "archive-remux",
            IsSingleFile: true,
            BundleDirectory: string.Empty,
            MasterPlaylistName: string.Empty,
            ManifestPath: "Fight Club.(1999).NoMercy.manifest.json",
            ReconstructionPath: "Fight Club.(1999).NoMercy.reconstruction.json",
            SingleFileName: "Fight Club.(1999).NoMercy.mkv",
            PresetId: "01HZDEF",
            PresetName: "Archive Remux MKV",
            ContainerString: "mkv"
        );
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class ReconstructionWriterTests
{
    private readonly ReconstructionWriter _writer = new();

    // -----------------------------------------------------------------------
    // Test 1: single-file copy preset → fidelity="lossless", no lossy warnings
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_CopyPreset_AllTracksLossless_NoWarnings()
    {
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo();
        BundleLayout layout = ReconstructionTestData.MkvLayout();

        OutputPlan plan = new(
            Format: OutputFormat.Mkv,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 800,
                    EncoderName: "copy",
                    Crf: 0,
                    BitrateKbps: 0,
                    Preset: null,
                    Profile: null,
                    Level: null,
                    TenBit: true,
                    PixelFormat: "yuv420p10le",
                    MapLabel: "0:v:0",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(
                    EncoderName: "copy",
                    BitrateKbps: 0,
                    Channels: 8,
                    SampleRate: 48_000,
                    Action: StreamAction.Copy,
                    Language: "eng",
                    MapLabel: "0:a:0"
                ),
            ],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Copy,
                    Action: StreamAction.Copy,
                    Language: "eng",
                    SourceIndex: 2,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.Copy
                ),
            ],
            Thumbnails: null,
            Layout: layout
        );

        Reconstruction result = _writer.Build(media, plan, layout);

        result.Tracks.Should().NotBeEmpty();
        result.Tracks.Should().AllSatisfy(t => t.Fidelity.Should().Be("lossless"));
        result.LossyWarnings.Should().BeEmpty();
        result.Source.OriginalPath.Should().Be("/media/fight-club.mkv");
        result.Source.SizeBytes.Should().Be(24_375_000_000L);
        result.Source.DurationSeconds.Should().BeApproximately(7800, 0.001);
        result.Source.Container.Should().Be("matroska");
    }

    // -----------------------------------------------------------------------
    // Test 2: HLS transcode preset → lossy per track, warnings list per lossy stream
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_HlsTranscode_LossyTracksAndWarnings()
    {
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo();
        BundleLayout layout = ReconstructionTestData.HlsLayout();

        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 800,
                    EncoderName: "libx264",
                    Crf: 18,
                    BitrateKbps: 4_000,
                    Preset: "slow",
                    Profile: "high",
                    Level: "4.1",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "0:v:0",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48_000,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "0:a:0"
                ),
            ],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Extract,
                    Language: "eng",
                    SourceIndex: 2,
                    MapLabel: "0:s:0",
                    Policy: SubtitlePolicy.Extract
                ),
            ],
            Thumbnails: null,
            Layout: layout
        );

        Reconstruction result = _writer.Build(media, plan, layout);

        ReconstructionTrack? videoTrack = result.Tracks.FirstOrDefault(t => t.Kind == "video");
        videoTrack.Should().NotBeNull();
        videoTrack!.Fidelity.Should().Be("lossy");
        videoTrack.LossyReason.Should().NotBeNullOrEmpty();

        ReconstructionTrack? audioTrack = result.Tracks.FirstOrDefault(t => t.Kind == "audio");
        audioTrack.Should().NotBeNull();
        audioTrack!.Fidelity.Should().Be("lossy");
        audioTrack.LossyReason.Should().NotBeNullOrEmpty();

        // ASS → WebVTT is a codec change → lossy (typesetting lost)
        ReconstructionTrack? subtitleTrack = result.Tracks.FirstOrDefault(t =>
            t.Kind == "subtitle"
        );
        subtitleTrack.Should().NotBeNull();
        subtitleTrack!.Fidelity.Should().Be("lossy");

        // One warning entry per lossy stream
        result.LossyWarnings.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // -----------------------------------------------------------------------
    // Test 3: burn-in subtitle → included with fidelity="irreversible"
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_BurnInSubtitle_IrreversibleFidelity()
    {
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo();
        BundleLayout layout = ReconstructionTestData.HlsLayout();

        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 800,
                    EncoderName: "libx264",
                    Crf: 18,
                    BitrateKbps: 4_000,
                    Preset: "slow",
                    Profile: "high",
                    Level: "4.1",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "0:v:0",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Drop,
                    Language: "eng",
                    SourceIndex: 2,
                    MapLabel: null,
                    Policy: SubtitlePolicy.BurnIn
                ),
            ],
            Thumbnails: null,
            Layout: layout
        );

        Reconstruction result = _writer.Build(media, plan, layout);

        ReconstructionTrack? burnTrack = result.Tracks.FirstOrDefault(t =>
            t.Kind == "subtitle" && t.SourceStreamIndex == 2
        );
        burnTrack.Should().NotBeNull("burn-in subtitle must appear in tracks");
        burnTrack!.Fidelity.Should().Be("irreversible");
        burnTrack.Policy.Should().Be("burn_in");
    }

    // -----------------------------------------------------------------------
    // Test 4: acquired subtitle → fidelity="external", policy="acquire"
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_AcquiredSubtitle_ExternalFidelity()
    {
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo();
        BundleLayout layout = ReconstructionTestData.HlsLayout();

        // Acquired subtitles are represented as SourceIndex == -1 (no source stream)
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.WebVtt,
                    Action: StreamAction.Extract,
                    Language: "fra",
                    SourceIndex: -1,
                    MapLabel: null,
                    Policy: SubtitlePolicy.Extract
                ),
            ],
            Thumbnails: null,
            Layout: layout
        );

        Reconstruction result = _writer.Build(media, plan, layout);

        ReconstructionTrack? acquiredTrack = result.Tracks.FirstOrDefault(t =>
            t.Kind == "subtitle" && t.SourceStreamIndex == -1
        );
        acquiredTrack.Should().NotBeNull("acquired subtitle must appear in tracks");
        acquiredTrack!.Fidelity.Should().Be("external");
        acquiredTrack.Policy.Should().Be("acquire");
    }

    // -----------------------------------------------------------------------
    // Test 5: source size + duration populated from analyzer output
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_Source_PopulatesFromMediaInfo()
    {
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo("/nas/films/fight-club.mkv");
        BundleLayout layout = ReconstructionTestData.MkvLayout();
        OutputPlan plan = new(
            Format: OutputFormat.Mkv,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Layout: layout
        );

        Reconstruction result = _writer.Build(media, plan, layout);

        result.Source.OriginalPath.Should().Be("/nas/films/fight-club.mkv");
        result.Source.OriginalFilename.Should().Be("fight-club.mkv");
        result.Source.SizeBytes.Should().Be(24_375_000_000L);
        result.Source.DurationSeconds.Should().BeApproximately(7800.0, 0.01);
        result.Source.Container.Should().Be("matroska");
        // sha256 is intentionally empty — deferred (see Reconstruction.cs TODO)
        result.Source.Sha256.Should().BeNullOrEmpty();
        result.Version.Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // Test 5b: a staged source is described by the file it stands for
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_Source_NamesTheRealSource_NotTheStagingLeaseItWasAnalysedThrough()
    {
        // A remote source is copied to a local lease so ffprobe can read it, and
        // the lease is released the moment the encode ends. MediaInfo therefore
        // describes a path that is about to stop existing — recording it would
        // leave the reconstruction unable to name what was encoded.
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo(
            @"C:\cache\temp\nomercy-remote-0da187041d6e4686ac8d683fb8cd4241"
        );
        BundleLayout layout = ReconstructionTestData.MkvLayout();
        OutputPlan plan = new(
            Format: OutputFormat.Mkv,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Layout: layout
        );

        Reconstruction result = _writer.Build(
            media,
            plan,
            layout,
            originalSourcePath: "Download/complete/Show/fight-club.mkv"
        );

        result.Source.OriginalPath.Should().Be("Download/complete/Show/fight-club.mkv");
        result.Source.OriginalFilename.Should().Be("fight-club.mkv");
    }

    [Fact]
    public void Build_Source_FallsBackToMediaInfo_WhenNothingWasStaged()
    {
        // A local source is analysed in place, so MediaInfo already names the real
        // file and there is no original to pass.
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo("/nas/films/fight-club.mkv");
        BundleLayout layout = ReconstructionTestData.MkvLayout();
        OutputPlan plan = new(
            Format: OutputFormat.Mkv,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Layout: layout
        );

        Reconstruction result = _writer.Build(media, plan, layout, originalSourcePath: null);

        result.Source.OriginalPath.Should().Be("/nas/films/fight-club.mkv");
    }

    // -----------------------------------------------------------------------
    // Test 6 (WriteAsync): writer serialises Reconstruction to storage
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WriteAsync_PersistsReconstructionJson()
    {
        TestStorage storage = new();
        ReconstructionWriter writer = new();
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo();
        BundleLayout layout = ReconstructionTestData.HlsLayout();
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Layout: layout
        );

        await writer.WriteAsync(
            storage,
            layout.ReconstructionPath,
            media,
            plan,
            layout,
            CancellationToken.None
        );

        string? json = storage.ReadString(layout.ReconstructionPath);
        json.Should().NotBeNull();

        Reconstruction? roundtrip = JsonConvert.DeserializeObject<Reconstruction>(json!);
        roundtrip.Should().NotBeNull();
        roundtrip!.Version.Should().Be(1);
        roundtrip.Source.Container.Should().Be("matroska");
    }

    // -----------------------------------------------------------------------
    // Test 7: ReadAsync returns null when missing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReadAsync_ReturnsNullWhenMissing()
    {
        TestStorage storage = new();
        ReconstructionWriter writer = new();

        Reconstruction? result = await writer.ReadAsync(
            storage,
            "encodes/web-1080p/reconstruction.json",
            CancellationToken.None
        );

        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Test 8: chapters emitted in external_assets
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_WithChapters_EmitsInExternalAssets()
    {
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo() with
        {
            Chapters =
            [
                new(TimeSpan.Zero, TimeSpan.FromMinutes(30), "Act 1"),
                new(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(90), "Act 2"),
            ],
        };
        BundleLayout layout = ReconstructionTestData.MkvLayout();
        OutputPlan plan = new(
            Format: OutputFormat.Mkv,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Layout: layout
        );

        Reconstruction result = _writer.Build(media, plan, layout);

        result.ExternalAssets.Chapters.Should().HaveCount(2);
        result.ExternalAssets.Chapters[0].Title.Should().Be("Act 1");
        result.ExternalAssets.Chapters[1].Title.Should().Be("Act 2");
    }

    // -----------------------------------------------------------------------
    // Test 9: font attachments → fonts list; non-font attachments → attachments list
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_WithAttachments_SeparatesFontsAndOtherAttachments()
    {
        MediaInfo media = ReconstructionTestData.MinimalMediaInfo() with
        {
            Attachments =
            [
                new(4, "ttf", "OpenSans.ttf", "application/x-truetype-font"),
                new(5, "ttf", "Roboto.ttf", "application/x-truetype-font"),
                new(6, "bin", "cover.jpg", "image/jpeg"),
            ],
        };
        BundleLayout layout = ReconstructionTestData.MkvLayout();
        OutputPlan plan = new(
            Format: OutputFormat.Mkv,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null,
            Layout: layout
        );

        Reconstruction result = _writer.Build(media, plan, layout);

        // Font-type MIME types route to Fonts
        result.ExternalAssets.Fonts.Should().HaveCount(2);
        result.ExternalAssets.Fonts.Should().Contain("OpenSans.ttf");
        result.ExternalAssets.Fonts.Should().Contain("Roboto.ttf");

        // Non-font attachments route to Attachments
        result.ExternalAssets.Attachments.Should().ContainSingle();
        result.ExternalAssets.Attachments.Should().Contain("cover.jpg");
    }
}
