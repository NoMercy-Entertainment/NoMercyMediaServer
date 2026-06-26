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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Output;

/// <summary>
/// Deepens <see cref="MkvOutputStrategyTests"/> with the per-branch
/// guarantees the existing presence-only tests don't enforce:
///
/// • Audio Copy vs Transcode policy — Copy emits literal `-c:a copy` and
///   skips bitrate; Transcode emits the encoder name and bitrate.
/// • Subtitle Copy with null MapLabel falls back to `0:s:{SourceIndex}` —
///   this fallback is the only path that keeps signal/SDH/forced track
///   ordering when PlanStage couldn't assign a label.
/// • Subtitle non-Copy actions (Extract, BurnIn, OcrExtract) MUST NOT add
///   a `-map` for the subtitle — MKV mux is for embedded copies only.
/// • PixelFormat gate — `-pix_fmt` only emitted on 10-bit outputs so 8-bit
///   ffmpeg presets aren't forced into yuv420p10le for no reason.
/// • Bitrate / CRF gates — zero/negative values are documented "unset"
///   sentinels; emitting `-crf 0` (lossless) by accident is a quality cliff.
/// • Audio filter wiring — `-af` only when the PRIMARY audio is Transcode
///   AND has a non-empty filter; Copy audio with a filter is a logic bug
///   we want to surface (filter would be silently dropped today).
/// • FinalizeAsync rename — source.mkv → mediaTitle.mkv when the source
///   exists AND target differs; no-op when source == target; pre-delete
///   target to avoid Move conflicts.
/// </summary>
public class MkvOutputStrategyBranchTests
{
    // ── ConfigureOutput audio policy ─────────────────────────────────────────

    [Fact]
    public void ConfigureOutput_audio_copy_emits_literal_copy_codec_no_bitrate()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan()],
                AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Copy, "eng", "0:a:0")],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().ContainInOrder("-c:a", "copy");
        cmd.Arguments.Should().NotContain("-b:a"); // Copy → no bitrate emitted
    }

    [Fact]
    public void ConfigureOutput_audio_transcode_emits_encoder_name_and_bitrate()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan()],
                AudioOutputs: [new("eac3", 384, 6, 48000, StreamAction.Transcode, "eng", "[a0]")],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().ContainInOrder("-c:a", "eac3");
        cmd.Arguments.Should().ContainInOrder("-b:a", "384k");
    }

    [Fact]
    public void ConfigureOutput_audio_drop_omits_audio_codec_and_map()
    {
        // Drop = don't include this track. No -map for this entry.
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan()],
                AudioOutputs: [new("aac", 192, 2, 48000, StreamAction.Drop, "eng", "0:a:0")],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().NotContain("0:a:0"); // Drop → no map
    }

    // ── ConfigureOutput subtitle policy ──────────────────────────────────────

    [Fact]
    public void ConfigureOutput_subtitle_copy_uses_map_label_when_set()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan()],
                AudioOutputs: [],
                SubtitleOutputs:
                [
                    new(
                        OutputCodec: SubtitleCodecType.Copy,
                        Action: StreamAction.Copy,
                        Language: "eng",
                        SourceIndex: 2,
                        MapLabel: "[s0]"
                    ),
                ],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().ContainInOrder("-map", "[s0]");
    }

    [Fact]
    public void ConfigureOutput_subtitle_copy_falls_back_to_source_index_when_map_label_null()
    {
        // The fallback path `0:s:{SourceIndex}` matters when PlanStage couldn't
        // assign a label — the test pins it because removing the fallback would
        // silently drop subtitle tracks from the MKV.
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan()],
                AudioOutputs: [],
                SubtitleOutputs:
                [
                    new(
                        OutputCodec: SubtitleCodecType.Copy,
                        Action: StreamAction.Copy,
                        Language: "eng",
                        SourceIndex: 4,
                        MapLabel: null
                    ),
                ],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().ContainInOrder("-map", "0:s:4");
    }

    [Theory]
    [InlineData(StreamAction.Extract)]
    [InlineData(StreamAction.Transcode)]
    [InlineData(StreamAction.Drop)]
    public void ConfigureOutput_subtitle_non_copy_actions_are_not_mapped(StreamAction action)
    {
        // MKV mux only carries Copy-action subtitles; everything else is handled
        // out-of-band (sidecar VTT extract, burn-in filter, drop). Mapping non-Copy
        // would either duplicate the track or crash ffmpeg.
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan()],
                AudioOutputs: [],
                SubtitleOutputs:
                [
                    new(
                        OutputCodec: SubtitleCodecType.WebVtt,
                        Action: action,
                        Language: "eng",
                        SourceIndex: 2,
                        MapLabel: "[s0]"
                    ),
                ],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().NotContain("[s0]");
        cmd.Arguments.Should().NotContain("0:s:2");
    }

    // ── ConfigureOutput video flags ──────────────────────────────────────────

    [Fact]
    public void ConfigureOutput_pix_fmt_only_emitted_when_video_is_ten_bit()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder8 = new();
        builder8.AddInput(new("/input.mkv"));
        strategy.ConfigureOutput(
            builder8,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan(tenBit: false, pixelFormat: "yuv420p")],
                AudioOutputs: [],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommandBuilder builder10 = new();
        builder10.AddInput(new("/input.mkv"));
        strategy.ConfigureOutput(
            builder10,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan(tenBit: true, pixelFormat: "yuv420p10le")],
                AudioOutputs: [],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        builder8.Build("ffmpeg").Arguments.Should().NotContain("-pix_fmt");
        builder10.Build("ffmpeg").Arguments.Should().ContainInOrder("-pix_fmt", "yuv420p10le");
    }

    [Fact]
    public void ConfigureOutput_crf_zero_is_treated_as_unset()
    {
        // CRF 0 means lossless to libx264 — but our profile uses 0 as the
        // "no CRF, use bitrate" sentinel. Emitting -crf 0 by accident is a
        // 50× file-size cliff.
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan(crf: 0, bitrateKbps: 5000)],
                AudioOutputs: [],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().NotContain("-crf");
        cmd.Arguments.Should().ContainInOrder("-b:v", "5000k");
    }

    [Fact]
    public void ConfigureOutput_bitrate_zero_is_treated_as_unset()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan(crf: 23, bitrateKbps: 0)],
                AudioOutputs: [],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().ContainInOrder("-crf", "23");
        cmd.Arguments.Should().NotContain("-b:v");
    }

    // ── ConfigureOutput audio filter wiring ──────────────────────────────────

    [Fact]
    public void ConfigureOutput_audio_filter_emitted_when_primary_audio_is_transcode_with_filter()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan()],
                AudioOutputs:
                [
                    new(
                        "aac",
                        192,
                        2,
                        48000,
                        StreamAction.Transcode,
                        "eng",
                        "[a0]",
                        AudioFilter: "loudnorm=I=-23:LRA=7:tp=-2"
                    ),
                ],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().ContainInOrder("-af", "loudnorm=I=-23:LRA=7:tp=-2");
    }

    [Fact]
    public void ConfigureOutput_audio_filter_dropped_when_primary_audio_is_copy()
    {
        // Documented current behavior: Copy + filter drops the filter silently.
        // Pinned so the change to either warn or move the filter elsewhere is
        // intentional, not accidental.
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan()],
                AudioOutputs:
                [
                    new(
                        "aac",
                        192,
                        2,
                        48000,
                        StreamAction.Copy,
                        "eng",
                        "0:a:0",
                        AudioFilter: "loudnorm=I=-23"
                    ),
                ],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().NotContain("-af");
    }

    [Fact]
    public void ConfigureOutput_audio_filter_empty_string_treated_as_no_filter()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [VideoPlan()],
                AudioOutputs:
                [
                    new(
                        "aac",
                        192,
                        2,
                        48000,
                        StreamAction.Transcode,
                        "eng",
                        "[a0]",
                        AudioFilter: ""
                    ),
                ],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments.Should().NotContain("-af");
    }

    // ── ConfigureOutput plan-without-streams edge cases ─────────────────────

    [Fact]
    public void ConfigureOutput_empty_plan_still_emits_output_path()
    {
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());
        FfmpegCommandBuilder builder = new();
        builder.AddInput(new("/input.mkv"));

        strategy.ConfigureOutput(
            builder,
            new(
                Format: OutputFormat.Mkv,
                VideoOutputs: [],
                AudioOutputs: [],
                SubtitleOutputs: [],
                Thumbnails: null
            ),
            "/output"
        );

        FfmpegCommand cmd = builder.Build("ffmpeg");
        cmd.Arguments[^1].Should().EndWith("output.mkv");
    }

    // ── FinalizeAsync rename behavior ────────────────────────────────────────

    [Fact]
    public async Task FinalizeAsync_renames_output_mkv_to_media_title_when_source_exists()
    {
        Mock<IStorage> storage = new();
        string sourceMkv = Path.Combine("/out", "output.mkv");
        string targetMkv = Path.Combine("/out", "MyMovie.mkv");
        storage.Setup(s => s.Exists(sourceMkv)).Returns(true);

        MkvOutputStrategy strategy = new(storage.Object);
        await strategy.FinalizeAsync("/out", EmptyPlan(), "MyMovie", ct: default);

        storage.Verify(s => s.Delete(targetMkv), Times.Once); // pre-delete target
        storage.Verify(s => s.Move(sourceMkv, targetMkv), Times.Once);
    }

    [Fact]
    public async Task FinalizeAsync_skips_rename_when_source_does_not_exist()
    {
        Mock<IStorage> storage = new();
        storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);

        MkvOutputStrategy strategy = new(storage.Object);
        await strategy.FinalizeAsync("/out", EmptyPlan(), "MyMovie", ct: default);

        storage.Verify(s => s.Move(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        storage.Verify(s => s.Delete(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FinalizeAsync_skips_rename_when_source_equals_target()
    {
        // If the media title is exactly "output", the paths coincide and Move
        // would be a no-op (or crash on some filesystems). Skip cleanly.
        Mock<IStorage> storage = new();
        storage.Setup(s => s.Exists(It.IsAny<string>())).Returns(true);

        MkvOutputStrategy strategy = new(storage.Object);
        await strategy.FinalizeAsync("/out", EmptyPlan(), "output", ct: default);

        storage.Verify(s => s.Move(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── GetOutputSubdirectories ──────────────────────────────────────────────

    [Fact]
    public void GetOutputSubdirectories_returns_empty_for_any_plan()
    {
        // MKV is a single-file container — no subdirectory layout. Multiple
        // calls with different plan shapes must all return empty.
        MkvOutputStrategy strategy = new(TestStorageFactory.CreateLocal());

        strategy.GetOutputSubdirectories(EmptyPlan()).Should().BeEmpty();
        strategy
            .GetOutputSubdirectories(
                new(
                    Format: OutputFormat.Mkv,
                    VideoOutputs: [VideoPlan(), VideoPlan(width: 1280, height: 720)],
                    AudioOutputs:
                    [
                        new("aac", 192, 2, 48000, StreamAction.Transcode, "eng", "[a0]"),
                        new("eac3", 384, 6, 48000, StreamAction.Transcode, "fre", "[a1]"),
                    ],
                    SubtitleOutputs:
                    [
                        new(SubtitleCodecType.Copy, StreamAction.Copy, "eng", 2, "[s0]"),
                    ],
                    Thumbnails: new ThumbnailOutputPlan(160, 90, 10)
                )
            )
            .Should()
            .BeEmpty();
    }

    // ── Format property ──────────────────────────────────────────────────────

    [Fact]
    public void Format_is_Mkv()
    {
        new MkvOutputStrategy(TestStorageFactory.CreateLocal())
            .Format.Should()
            .Be(OutputFormat.Mkv);
    }

    // ── Builders ─────────────────────────────────────────────────────────────

    private static VideoOutputPlan VideoPlan(
        int width = 1920,
        int height = 1080,
        int crf = 23,
        int bitrateKbps = 0,
        bool tenBit = false,
        string pixelFormat = "yuv420p"
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: "libx264",
            Crf: crf,
            BitrateKbps: bitrateKbps,
            Preset: "medium",
            Profile: "high",
            Level: "4.0",
            TenBit: tenBit,
            PixelFormat: pixelFormat,
            MapLabel: "[v0]",
            ExtraFlags: []
        );

    private static OutputPlan EmptyPlan() =>
        new(
            Format: OutputFormat.Mkv,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );
}
