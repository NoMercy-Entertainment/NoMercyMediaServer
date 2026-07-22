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

public class HardwarePreferenceResolverTests
{
    private readonly HardwarePreferenceResolver _resolver = new();

    // ── helpers ──────────────────────────────────────────────────────────────

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

    private static List<string> NoHwEncoders() => ["libx264", "libx265", "libsvtav1"];

    private static List<string> WithNvenc() =>
        ["libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc"];

    private static ScopedDecisionLog NewLog() => new();

    // ── ForceSoftware ─────────────────────────────────────────────────────────

    [Fact]
    public void ForceSoftware_picks_libx264_for_H264_even_when_NVENC_available()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 120), (VideoCodecType.H264, "h264_nvenc", 600)]
        );

        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: WithNvenc(),
            speedIndex: index,
            decisions: log
        );

        Assert.Null(@object: result.Failure);
        Assert.Equal(expected: "libx264", actual: result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Single(collection: snapshot);
        Assert.Contains(expectedSubstring: "ForceSoftware", actualString: snapshot[index: 0].Message);
        Assert.Contains(expectedSubstring: "libx264", actualString: snapshot[index: 0].Message);
    }

    [Fact]
    public void ForceSoftware_picks_libx265_for_HEVC()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H265,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: NoHwEncoders(),
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        Assert.Null(@object: result.Failure);
        Assert.Equal(expected: "libx265", actual: result.EncoderHandle);
    }

    [Fact]
    public void ForceSoftware_picks_libsvtav1_for_AV1()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.Av1,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: NoHwEncoders(),
            speedIndex: EmptyIndex(),
            decisions: NewLog()
        );

        Assert.Null(@object: result.Failure);
        Assert.Equal(expected: "libsvtav1", actual: result.EncoderHandle);
    }

    // ── PreferQuality ─────────────────────────────────────────────────────────

    [Fact]
    public void PreferQuality_behaves_like_ForceSoftware()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 120), (VideoCodecType.H264, "h264_nvenc", 600)]
        );

        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferQuality,
            availableEncoders: WithNvenc(),
            speedIndex: index,
            decisions: log
        );

        Assert.Null(@object: result.Failure);
        Assert.Equal(expected: "libx264", actual: result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Single(collection: snapshot);
        Assert.Contains(expectedSubstring: "PreferQuality", actualString: snapshot[index: 0].Message);
        // Should mention that HW was available but not chosen
        Assert.Contains(expectedSubstring: "HW available", actualString: snapshot[index: 0].Message);
    }

    // ── PreferHardware ────────────────────────────────────────────────────────

    [Fact]
    public void PreferHardware_picks_NVENC_when_speed_index_higher()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 120), (VideoCodecType.H264, "h264_nvenc", 600)]
        );

        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: WithNvenc(),
            speedIndex: index,
            decisions: log
        );

        Assert.Null(@object: result.Failure);
        Assert.Equal(expected: "h264_nvenc", actual: result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Single(collection: snapshot);
        Assert.Contains(expectedSubstring: "h264_nvenc", actualString: snapshot[index: 0].Message);
        Assert.Contains(expectedSubstring: "over libx264", actualString: snapshot[index: 0].Message);
    }

    [Fact]
    public void PreferHardware_falls_back_to_software_when_no_HW_entries()
    {
        SpeedIndex index = MakeSpeedIndex(entries: (VideoCodecType.H264, "libx264", 120));

        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: NoHwEncoders(),
            speedIndex: index,
            decisions: log
        );

        Assert.Null(@object: result.Failure);
        Assert.Equal(expected: "libx264", actual: result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Single(collection: snapshot);
        Assert.Contains(expectedSubstring: "no HW encoder available", actualString: snapshot[index: 0].Message);
    }

    [Fact]
    public void PreferHardware_picks_unmeasured_HW_from_availableEncoders_when_index_is_empty()
    {
        // Lazy benchmark hasn't populated SpeedIndex yet, but availableEncoders
        // exposes hevc_nvenc — resolver should pick it rather than dropping to
        // libx265.
        SpeedIndex index = EmptyIndex();

        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H265,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: WithNvenc(),
            speedIndex: index,
            decisions: log
        );

        Assert.Null(@object: result.Failure);
        Assert.Equal(expected: "hevc_nvenc", actual: result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Contains(expectedSubstring: "no benchmark yet", actualString: snapshot[index: 0].Message);
    }

    // ── ForceHardware ─────────────────────────────────────────────────────────

    [Fact]
    public void ForceHardware_fails_when_no_HW_available()
    {
        SpeedIndex index = MakeSpeedIndex(entries: (VideoCodecType.H264, "libx264", 120));

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: NoHwEncoders(),
            speedIndex: index,
            decisions: NewLog()
        );

        Assert.NotNull(@object: result.Failure);
        Assert.Null(@object: result.EncoderHandle);
        Assert.Equal(expected: 422, actual: result.Failure.HttpStatusCode);
        Assert.Equal(expected: EncoderRuleId.HardwareForcedButUnavailable, actual: result.Failure.Shape.Id);
    }

    [Fact]
    public void ForceHardware_picks_highest_HW_entry_when_multiple()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 120), (VideoCodecType.H264, "h264_nvenc", 600), (VideoCodecType.H264, "h264_qsv", 300), (VideoCodecType.H264, "h264_amf", 450)]
        );

        HardwareResolutionResult result = _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: WithNvenc(),
            speedIndex: index,
            decisions: NewLog()
        );

        Assert.Null(@object: result.Failure);
        Assert.Equal(expected: "h264_nvenc", actual: result.EncoderHandle);
    }

    // ── Decision log coverage ─────────────────────────────────────────────────

    [Fact]
    public void Decision_log_includes_reason_for_each_branch()
    {
        SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 120), (VideoCodecType.H264, "h264_nvenc", 600)]
        );

        // ForceSoftware
        ScopedDecisionLog fsLog = NewLog();
        _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceSoftware,
            availableEncoders: WithNvenc(),
            speedIndex: index,
            decisions: fsLog
        );
        Assert.Contains(expectedSubstring: "force_software", actualString: fsLog.Snapshot()[index: 0].Message + fsLog.Snapshot()[index: 0].Data);

        // PreferQuality
        ScopedDecisionLog pqLog = NewLog();
        _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferQuality,
            availableEncoders: WithNvenc(),
            speedIndex: index,
            decisions: pqLog
        );
        Assert.Contains(expectedSubstring: "PreferQuality", actualString: pqLog.Snapshot()[index: 0].Message);

        // PreferHardware
        ScopedDecisionLog phLog = NewLog();
        _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.PreferHardware,
            availableEncoders: WithNvenc(),
            speedIndex: index,
            decisions: phLog
        );
        Assert.Contains(expectedSubstring: "PreferHardware", actualString: phLog.Snapshot()[index: 0].Message);

        // ForceHardware
        ScopedDecisionLog fhLog = NewLog();
        _resolver.Resolve(
            codec: VideoCodecType.H264,
            preference: HardwarePreference.ForceHardware,
            availableEncoders: WithNvenc(),
            speedIndex: index,
            decisions: fhLog
        );
        Assert.Contains(expectedSubstring: "ForceHardware", actualString: fhLog.Snapshot()[index: 0].Message);
    }
}
