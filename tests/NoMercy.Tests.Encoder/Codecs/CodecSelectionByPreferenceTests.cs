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
            SpeedKey key = new(codec, encoder, 1920, null);
            dict[key] = new(fps, 1.0, DateTime.UtcNow);
        }
        return new(dict);
    }

    private static SpeedIndex EmptyIndex() => new(new());

    private static ScopedDecisionLog NewLog() => new();

    // ── Copy codec ──────────────────────────────────────────────────────────────

    [Fact]
    public void Copy_Returns_Copy_Handle_Regardless_Of_Preference()
    {
        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.Copy,
            HardwarePreference.ForceHardware,
            ["copy"],
            EmptyIndex(),
            log
        );

        result.EncoderHandle.Should().Be("copy");
        result.Failure.Should().BeNull();
    }

    [Fact]
    public void Copy_Logs_Decision_Correctly()
    {
        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            VideoCodecType.Copy,
            HardwarePreference.PreferHardware,
            ["copy"],
            EmptyIndex(),
            log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs.Should().HaveCount(1);
        logs[0].Message.Should().Contain("copy");
    }

    // ── ForceSoftware ───────────────────────────────────────────────────────────

    [Fact]
    public void ForceSoftware_H264_Returns_Libx264()
    {
        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceSoftware,
            ["libx264", "h264_nvenc"],
            EmptyIndex(),
            log
        );

        result.EncoderHandle.Should().Be("libx264");
        result.Failure.Should().BeNull();
    }

    [Fact]
    public void ForceSoftware_H265_Returns_Libx265()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H265,
            HardwarePreference.ForceSoftware,
            ["libx265", "hevc_nvenc"],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().Be("libx265");
    }

    [Fact]
    public void ForceSoftware_Av1_Returns_Libsvtav1()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.Av1,
            HardwarePreference.ForceSoftware,
            ["libsvtav1", "av1_nvenc"],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().Be("libsvtav1");
    }

    [Fact]
    public void ForceSoftware_Vp9_Returns_Libvpxvp9()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.Vp9,
            HardwarePreference.ForceSoftware,
            ["libvpx-vp9", "vp9_qsv"],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().Be("libvpx-vp9");
    }

    [Fact]
    public void ForceSoftware_Logs_Reason()
    {
        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceSoftware,
            ["libx264"],
            EmptyIndex(),
            log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs[0].Message.Should().Contain("ForceSoftware");
        logs[0].Message.Should().Contain("libx264");
    }

    // ── PreferQuality ───────────────────────────────────────────────────────────

    [Fact]
    public void PreferQuality_Returns_Software_Even_When_Hw_Available()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 100),
            (VideoCodecType.H264, "h264_nvenc", 500)
        );

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferQuality,
            ["libx264", "h264_nvenc"],
            index,
            NewLog()
        );

        result.EncoderHandle.Should().Be("libx264");
    }

    [Fact]
    public void PreferQuality_Logs_That_Hw_Was_Available()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 100),
            (VideoCodecType.H264, "h264_nvenc", 500)
        );

        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferQuality,
            ["libx264", "h264_nvenc"],
            index,
            log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs[0].Message.Should().Contain("HW available");
    }

    [Fact]
    public void PreferQuality_H265_Returns_Libx265()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H265,
            HardwarePreference.PreferQuality,
            ["libx265", "hevc_nvenc"],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().Be("libx265");
    }

    // ── PreferHardware (speed benchmark available) ──────────────────────────────

    [Fact]
    public void PreferHardware_Picks_Faster_Hardware_When_Benchmarked()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 100),
            (VideoCodecType.H264, "h264_nvenc", 500)
        );

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferHardware,
            ["libx264", "h264_nvenc"],
            index,
            NewLog()
        );

        result.EncoderHandle.Should().Be("h264_nvenc");
    }

    [Fact]
    public void PreferHardware_Picks_Best_Hardware_Among_Multiple()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H265, "libx265", 80),
            (VideoCodecType.H265, "hevc_nvenc", 300),
            (VideoCodecType.H265, "hevc_qsv", 250),
            (VideoCodecType.H265, "hevc_amf", 400)
        );

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H265,
            HardwarePreference.PreferHardware,
            ["libx265", "hevc_nvenc", "hevc_qsv", "hevc_amf"],
            index,
            NewLog()
        );

        result.EncoderHandle.Should().Be("hevc_amf");
    }

    [Fact]
    public void PreferHardware_No_Benchmark_But_Hw_Available_Uses_Hw()
    {
        SpeedIndex emptyIndex = EmptyIndex();

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferHardware,
            ["libx264", "h264_nvenc"],
            emptyIndex,
            NewLog()
        );

        result.EncoderHandle.Should().Be("h264_nvenc");
    }

    [Fact]
    public void PreferHardware_No_Benchmark_No_Hw_Falls_To_Software()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.Vp9,
            HardwarePreference.PreferHardware,
            ["libvpx-vp9"],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().Be("libvpx-vp9");
    }

    [Fact]
    public void PreferHardware_Ignores_Unavailable_Benchmarks()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 100),
            (VideoCodecType.H264, "h264_nvenc", 500)
        );

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferHardware,
            ["libx264", "h264_qsv"],
            index,
            NewLog()
        );

        result.EncoderHandle.Should().Be("h264_qsv");
    }

    // ── ForceHardware ───────────────────────────────────────────────────────────

    [Fact]
    public void ForceHardware_Returns_Best_Hw_When_Benchmarked()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "h264_nvenc", 500),
            (VideoCodecType.H264, "h264_qsv", 300)
        );

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceHardware,
            ["h264_nvenc", "h264_qsv"],
            index,
            NewLog()
        );

        result.EncoderHandle.Should().Be("h264_nvenc");
    }

    [Fact]
    public void ForceHardware_No_Benchmark_But_Hw_Available_Uses_Hw()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H265,
            HardwarePreference.ForceHardware,
            ["libx265", "hevc_nvenc"],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().Be("hevc_nvenc");
    }

    [Fact]
    public void ForceHardware_No_Hw_Available_Returns_Failure()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.Vp9,
            HardwarePreference.ForceHardware,
            ["libvpx-vp9"],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().BeNull();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public void ForceHardware_Failure_Message_Names_Codec()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.Av1,
            HardwarePreference.ForceHardware,
            ["libsvtav1"],
            EmptyIndex(),
            NewLog()
        );

        result.Failure!.Message.Should().Contain("Av1");
    }

    [Fact]
    public void ForceHardware_Logs_Failure()
    {
        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceHardware,
            ["libx264"],
            EmptyIndex(),
            log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs[0].Message.Should().Contain("FAILED");
    }

    // ── Codec-to-encoder prefix matching ────────────────────────────────────────

    [Theory]
    [InlineData(VideoCodecType.H264, "h264_nvenc", true)]
    [InlineData(VideoCodecType.H264, "h264_qsv", true)]
    [InlineData(VideoCodecType.H264, "h264_amf", true)]
    [InlineData(VideoCodecType.H264, "h264_vaapi", true)]
    [InlineData(VideoCodecType.H264, "h264_videotoolbox", true)]
    [InlineData(VideoCodecType.H264, "hevc_nvenc", false)]
    [InlineData(VideoCodecType.H264, "libx265", false)]
    public void Codec_Matching_By_Prefix_Works_Correctly(
        VideoCodecType codec,
        string handle,
        bool shouldMatch
    )
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec,
            HardwarePreference.PreferHardware,
            [handle],
            EmptyIndex(),
            NewLog()
        );

        if (shouldMatch)
            result.EncoderHandle.Should().Be(handle);
        else
            result.EncoderHandle.Should().NotBe(handle);
    }

    [Theory]
    [InlineData(VideoCodecType.H265, "hevc_nvenc")]
    [InlineData(VideoCodecType.H265, "hevc_qsv")]
    [InlineData(VideoCodecType.H265, "hevc_amf")]
    [InlineData(VideoCodecType.H265, "hevc_vaapi")]
    [InlineData(VideoCodecType.H265, "hevc_videotoolbox")]
    public void H265_Matches_Hevc_Prefix_In_Encoder_Names(VideoCodecType codec, string handle)
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec,
            HardwarePreference.PreferHardware,
            [handle],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().Be(handle);
    }

    [Theory]
    [InlineData(VideoCodecType.Av1, "av1_nvenc")]
    [InlineData(VideoCodecType.Av1, "av1_qsv")]
    [InlineData(VideoCodecType.Av1, "av1_amf")]
    public void Av1_Matches_Av1_Prefix_In_Encoder_Names(VideoCodecType codec, string handle)
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec,
            HardwarePreference.PreferHardware,
            [handle],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().Be(handle);
    }

    [Theory]
    [InlineData(VideoCodecType.Vp9, "vp9_qsv")]
    public void Vp9_Matches_Vp9_Prefix_In_Encoder_Names(VideoCodecType codec, string handle)
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec,
            HardwarePreference.PreferHardware,
            [handle],
            EmptyIndex(),
            NewLog()
        );

        result.EncoderHandle.Should().Be(handle);
    }

    // ── Stale benchmark entries (encoder deleted from ffmpeg) ──────────────────

    [Fact]
    public void PreferHardware_Ignores_Benchmarked_But_Unavailable_Encoder()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 100),
            (VideoCodecType.H264, "h264_nvenc", 500)
        );

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferHardware,
            ["libx264", "h264_qsv"],
            index,
            NewLog()
        );

        result.EncoderHandle.Should().Be("h264_qsv");
    }

    [Fact]
    public void ForceHardware_Ignores_Benchmarked_But_Unavailable_Encoder()
    {
        SpeedIndex index = MakeSpeedIndex((VideoCodecType.H264, "h264_nvenc", 500));

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceHardware,
            ["libx264", "h264_qsv"],
            index,
            NewLog()
        );

        result.EncoderHandle.Should().Be("h264_qsv");
    }

    // ── Decision log tracking ───────────────────────────────────────────────────

    [Fact]
    public void All_Branches_Log_A_Decision()
    {
        foreach (HardwarePreference pref in Enum.GetValues<HardwarePreference>())
        {
            ScopedDecisionLog log = NewLog();

            _resolver.Resolve(
                VideoCodecType.H264,
                pref,
                ["libx264", "h264_nvenc"],
                MakeSpeedIndex(
                    (VideoCodecType.H264, "libx264", 100),
                    (VideoCodecType.H264, "h264_nvenc", 500)
                ),
                log
            );

            IReadOnlyList<DecisionLog> logs = log.Snapshot();
            logs.Should().HaveCount(1, $"preference {pref} should log a decision");
        }
    }

    [Fact]
    public void Decision_Log_Includes_Codec_Name()
    {
        ScopedDecisionLog log = NewLog();

        _resolver.Resolve(
            VideoCodecType.H265,
            HardwarePreference.ForceSoftware,
            ["libx265"],
            EmptyIndex(),
            log
        );

        IReadOnlyList<DecisionLog> logs = log.Snapshot();
        logs[0].Data.Should().NotBeNull();
    }
}
