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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using HardwarePreference = NoMercy.Encoder.Profiles.HardwarePreference;

namespace NoMercy.Tests.Encoder.Codecs;

/// <summary>
/// Tests encoder selection based on hardware preference policy and speed benchmarks.
/// Asserts that preference branches resolve to the correct encoder handle
/// (software fallback, speed-optimized HW, quality-optimized SW, unmeasured HW).
/// </summary>
public class CodecSelectionByPreferenceTests
{
    private readonly HardwarePreferenceResolver _resolver = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SpeedIndex MakeSpeedIndex(
        params (VideoCodecType Codec, string Encoder, double Fps)[] entries
    )
    {
        Dictionary<SpeedKey, SpeedMeasurement> dict = new();
        foreach ((VideoCodecType codec, string encoder, double fps) in entries)
        {
            SpeedKey key = new(Codec: codec, Encoder: encoder, Width: 1920, DeviceName: null);
            dict[key: key] = new(Fps: fps, SpeedMultiplier: 1.0, MeasuredAt: DateTime.UtcNow);
        }
        return new(Measurements: dict);
    }

    private static SpeedIndex EmptyIndex() => new(Measurements: new());

    private static ScopedDecisionLog NewLog() => new();

    // ── Copy codec ──────────────────────────────────────────────────────────────

    [Fact]
    public void Copy_Returns_Copy_Handle_Regardless_Of_Preference()
    {
        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.Copy,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: ["copy"],
            speedIndex: EmptyIndex(),
            decisions: log
        );

        result.EncoderHandle.Should().Be(expected: "copy");
        result.Failure.Should().BeNull();
    }

    [Fact]
    public void Copy_Logs_Decision_Correctly()
    {
        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            codec: VideoCodecType.Copy,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: ["copy"],
            speedIndex: EmptyIndex(),
            decisions: log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs.Should().HaveCount(expected: 1);
        logs[index: 0].Message.Should().Contain(expected: "copy");
    }

    // ── ForceSoftware ───────────────────────────────────────────────────────────

    [Fact]
    public void ForceSoftware_H264_Returns_Libx264()
    {
        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: ["libx264", "h264_nvenc"],
            speedIndex: EmptyIndex(),
            decisions: log
        );

        result.EncoderHandle.Should().Be(expected: "libx264");
        result.Failure.Should().BeNull();
    }

    [Fact]
    public void ForceSoftware_H265_Returns_Libx265()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H265,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: ["libx265", "hevc_nvenc"],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "libx265");
    }

    [Fact]
    public void ForceSoftware_Av1_Returns_Libsvtav1()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.Av1,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: ["libsvtav1", "av1_nvenc"],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "libsvtav1");
    }

    [Fact]
    public void ForceSoftware_Vp9_Returns_Libvpxvp9()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.Vp9,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: ["libvpx-vp9", "vp9_qsv"],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "libvpx-vp9");
    }

    [Fact]
    public void ForceSoftware_Logs_Reason()
    {
        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: ["libx264"],
            speedIndex: EmptyIndex(),
            decisions: log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs[index: 0].Message.Should().Contain(expected: "ForceSoftware");
        logs[index: 0].Message.Should().Contain(expected: "libx264");
    }

    // ── PreferQuality ───────────────────────────────────────────────────────────

    [Fact]
    public void PreferQuality_Returns_Software_Even_When_Hw_Available()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 500)]
        );

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferQuality,
            availableEncoders: ["libx264", "h264_nvenc"],
            speedIndex: index,
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "libx264");
    }

    [Fact]
    public void PreferQuality_Logs_That_Hw_Was_Available()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 500)]
        );

        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferQuality,
            availableEncoders: ["libx264", "h264_nvenc"],
            speedIndex: index,
            decisions: log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs[index: 0].Message.Should().Contain(expected: "HW available");
    }

    [Fact]
    public void PreferQuality_H265_Returns_Libx265()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H265,
            preference: HardwarePreference.PreferQuality,
            availableEncoders: ["libx265", "hevc_nvenc"],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "libx265");
    }

    // ── PreferHardware (speed benchmark available) ──────────────────────────────

    [Fact]
    public void PreferHardware_Picks_Faster_Hardware_When_Benchmarked()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 500)]
        );

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: ["libx264", "h264_nvenc"],
            speedIndex: index,
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "h264_nvenc");
    }

    [Fact]
    public void PreferHardware_Picks_Best_Hardware_Among_Multiple()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H265, "libx265", 80), (VideoCodecType.H265, "hevc_nvenc", 300), (VideoCodecType.H265, "hevc_qsv", 250), (VideoCodecType.H265, "hevc_amf", 400)]
        );

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H265,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: ["libx265", "hevc_nvenc", "hevc_qsv", "hevc_amf"],
            speedIndex: index,
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "hevc_amf");
    }

    [Fact]
    public void PreferHardware_No_Benchmark_But_Hw_Available_Uses_Hw()
    {
        SpeedIndex emptyIndex = EmptyIndex();

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: ["libx264", "h264_nvenc"],
            speedIndex: emptyIndex,
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "h264_nvenc");
    }

    [Fact]
    public void PreferHardware_No_Benchmark_No_Hw_Falls_To_Software()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.Vp9,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: ["libvpx-vp9"],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "libvpx-vp9");
    }

    [Fact]
    public void PreferHardware_Ignores_Unavailable_Benchmarks()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 500)]
        );

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: ["libx264", "h264_qsv"],
            speedIndex: index,
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "h264_qsv");
    }

    // ── ForceHardware ───────────────────────────────────────────────────────────

    [Fact]
    public void ForceHardware_Returns_Best_Hw_When_Benchmarked()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "h264_nvenc", 500), (VideoCodecType.H264, "h264_qsv", 300)]
        );

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: ["h264_nvenc", "h264_qsv"],
            speedIndex: index,
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "h264_nvenc");
    }

    [Fact]
    public void ForceHardware_No_Benchmark_But_Hw_Available_Uses_Hw()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H265,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: ["libx265", "hevc_nvenc"],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "hevc_nvenc");
    }

    [Fact]
    public void ForceHardware_No_Hw_Available_Returns_Failure()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.Vp9,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: ["libvpx-vp9"],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().BeNull();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public void ForceHardware_Failure_Message_Names_Codec()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.Av1,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: ["libsvtav1"],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.Failure!.Message.Should().Contain(expected: "Av1");
    }

    [Fact]
    public void ForceHardware_Logs_Failure()
    {
        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: ["libx264"],
            speedIndex: EmptyIndex(),
            decisions: log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs[index: 0].Message.Should().Contain(expected: "FAILED");
    }

    // ── Codec-to-encoder prefix matching ────────────────────────────────────────

    [Theory]
    [InlineData(data: [VideoCodecType.H264, "h264_nvenc", true])]
    [InlineData(data: [VideoCodecType.H264, "h264_qsv", true])]
    [InlineData(data: [VideoCodecType.H264, "h264_amf", true])]
    [InlineData(data: [VideoCodecType.H264, "h264_vaapi", true])]
    [InlineData(data: [VideoCodecType.H264, "h264_videotoolbox", true])]
    [InlineData(data: [VideoCodecType.H264, "hevc_nvenc", false])]
    [InlineData(data: [VideoCodecType.H264, "libx265", false])]
    public void Codec_Matching_By_Prefix_Works_Correctly(
        VideoCodecType codec,
        string handle,
        bool shouldMatch
    )
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: codec,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: [handle],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        if (shouldMatch)
            result.EncoderHandle.Should().Be(expected: handle);
        else
            result.EncoderHandle.Should().NotBe(unexpected: handle);
    }

    [Theory]
    [InlineData(data: [VideoCodecType.H265, "hevc_nvenc"])]
    [InlineData(data: [VideoCodecType.H265, "hevc_qsv"])]
    [InlineData(data: [VideoCodecType.H265, "hevc_amf"])]
    [InlineData(data: [VideoCodecType.H265, "hevc_vaapi"])]
    [InlineData(data: [VideoCodecType.H265, "hevc_videotoolbox"])]
    public void H265_Matches_Hevc_Prefix_In_Encoder_Names(VideoCodecType codec, string handle)
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: codec,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: [handle],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: handle);
    }

    [Theory]
    [InlineData(data: [VideoCodecType.Av1, "av1_nvenc"])]
    [InlineData(data: [VideoCodecType.Av1, "av1_qsv"])]
    [InlineData(data: [VideoCodecType.Av1, "av1_amf"])]
    public void Av1_Matches_Av1_Prefix_In_Encoder_Names(VideoCodecType codec, string handle)
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: codec,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: [handle],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: handle);
    }

    [Theory]
    [InlineData(data: [VideoCodecType.Vp9, "vp9_qsv"])]
    public void Vp9_Matches_Vp9_Prefix_In_Encoder_Names(VideoCodecType codec, string handle)
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: codec,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: [handle],
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: handle);
    }

    // ── Stale benchmark entries (encoder deleted from ffmpeg) ──────────────────

    [Fact]
    public void PreferHardware_Ignores_Benchmarked_But_Unavailable_Encoder()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 500)]
        );

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: ["libx264", "h264_qsv"],
            speedIndex: index,
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "h264_qsv");
    }

    [Fact]
    public void ForceHardware_Ignores_Benchmarked_But_Unavailable_Encoder()
    {
        SpeedIndex index = MakeSpeedIndex(entries: (VideoCodecType.H264, "h264_nvenc", 500));

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: ["libx264", "h264_qsv"],
            speedIndex: index,
            decisions: NewLog()
        );

        result.EncoderHandle.Should().Be(expected: "h264_qsv");
    }

    // ── Decision log tracking ───────────────────────────────────────────────────

    [Fact]
    public void All_Branches_Log_A_Decision()
    {
        foreach (HardwarePreference pref in Enum.GetValues<HardwarePreference>())
        {
            ScopedDecisionLog log = NewLog();

            _resolver.Resolve(
                codec: VideoCodecType.H264,
                preference: pref,
                availableEncoders: ["libx264", "h264_nvenc"],
                speedIndex: MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 500)]
                ),
                decisions: log
            );

            IReadOnlyList<DecisionLog> logs = log.Snapshot();
            logs.Should().HaveCount(expected: 1, because: $"preference {pref} should log a decision");
        }
    }

    [Fact]
    public void Decision_Log_Includes_Codec_Name()
    {
        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            codec: VideoCodecType.H265,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: ["libx265"],
            speedIndex: EmptyIndex(),
            decisions: log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs[index: 0].Data.Should().NotBeNull();
    }
}
