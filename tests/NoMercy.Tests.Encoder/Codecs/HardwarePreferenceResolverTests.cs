using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
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
            SpeedKey key = new(codec, encoder, 1920, null);
            dict[key] = new SpeedMeasurement(fps, 1.0, DateTime.UtcNow);
        }

        return new SpeedIndex(dict);
    }

    private static SpeedIndex EmptyIndex() => new(new Dictionary<SpeedKey, SpeedMeasurement>());

    private static List<string> NoHwEncoders() => ["libx264", "libx265", "libsvtav1"];

    private static List<string> WithNvenc() =>
        ["libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc"];

    private static ScopedDecisionLog NewLog() => new();

    // ── ForceSoftware ─────────────────────────────────────────────────────────

    [Fact]
    public void ForceSoftware_picks_libx264_for_H264_even_when_NVENC_available()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 120),
            (VideoCodecType.H264, "h264_nvenc", 600)
        );

        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceSoftware,
            WithNvenc(),
            index,
            log
        );

        Assert.Null(result.Failure);
        Assert.Equal("libx264", result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Single(snapshot);
        Assert.Contains("ForceSoftware", snapshot[0].Message);
        Assert.Contains("libx264", snapshot[0].Message);
    }

    [Fact]
    public void ForceSoftware_picks_libx265_for_HEVC()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H265,
            HardwarePreference.ForceSoftware,
            NoHwEncoders(),
            EmptyIndex(),
            NewLog()
        );

        Assert.Null(result.Failure);
        Assert.Equal("libx265", result.EncoderHandle);
    }

    [Fact]
    public void ForceSoftware_picks_libsvtav1_for_AV1()
    {
        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.Av1,
            HardwarePreference.ForceSoftware,
            NoHwEncoders(),
            EmptyIndex(),
            NewLog()
        );

        Assert.Null(result.Failure);
        Assert.Equal("libsvtav1", result.EncoderHandle);
    }

    // ── PreferQuality ─────────────────────────────────────────────────────────

    [Fact]
    public void PreferQuality_behaves_like_ForceSoftware()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 120),
            (VideoCodecType.H264, "h264_nvenc", 600)
        );

        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferQuality,
            WithNvenc(),
            index,
            log
        );

        Assert.Null(result.Failure);
        Assert.Equal("libx264", result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Single(snapshot);
        Assert.Contains("PreferQuality", snapshot[0].Message);
        // Should mention that HW was available but not chosen
        Assert.Contains("HW available", snapshot[0].Message);
    }

    // ── PreferHardware ────────────────────────────────────────────────────────

    [Fact]
    public void PreferHardware_picks_NVENC_when_speed_index_higher()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 120),
            (VideoCodecType.H264, "h264_nvenc", 600)
        );

        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferHardware,
            WithNvenc(),
            index,
            log
        );

        Assert.Null(result.Failure);
        Assert.Equal("h264_nvenc", result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Single(snapshot);
        Assert.Contains("h264_nvenc", snapshot[0].Message);
        Assert.Contains("over libx264", snapshot[0].Message);
    }

    [Fact]
    public void PreferHardware_falls_back_to_software_when_no_HW_entries()
    {
        SpeedIndex index = MakeSpeedIndex((VideoCodecType.H264, "libx264", 120));

        ScopedDecisionLog log = NewLog();

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferHardware,
            NoHwEncoders(),
            index,
            log
        );

        Assert.Null(result.Failure);
        Assert.Equal("libx264", result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Single(snapshot);
        Assert.Contains("no HW encoder available", snapshot[0].Message);
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
            VideoCodecType.H265,
            HardwarePreference.PreferHardware,
            WithNvenc(),
            index,
            log
        );

        Assert.Null(result.Failure);
        Assert.Equal("hevc_nvenc", result.EncoderHandle);

        IReadOnlyList<DecisionLog> snapshot = log.Snapshot();
        Assert.Contains("no benchmark yet", snapshot[0].Message);
    }

    // ── ForceHardware ─────────────────────────────────────────────────────────

    [Fact]
    public void ForceHardware_fails_when_no_HW_available()
    {
        SpeedIndex index = MakeSpeedIndex((VideoCodecType.H264, "libx264", 120));

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceHardware,
            NoHwEncoders(),
            index,
            NewLog()
        );

        Assert.NotNull(result.Failure);
        Assert.Null(result.EncoderHandle);
        Assert.Equal(422, result.Failure.HttpStatusCode);
        Assert.Equal(EncoderRuleId.HardwareForcedButUnavailable, result.Failure.Shape.Id);
    }

    [Fact]
    public void ForceHardware_picks_highest_HW_entry_when_multiple()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 120),
            (VideoCodecType.H264, "h264_nvenc", 600),
            (VideoCodecType.H264, "h264_qsv", 300),
            (VideoCodecType.H264, "h264_amf", 450)
        );

        HardwareResolutionResult result = _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceHardware,
            WithNvenc(),
            index,
            NewLog()
        );

        Assert.Null(result.Failure);
        Assert.Equal("h264_nvenc", result.EncoderHandle);
    }

    // ── Decision log coverage ─────────────────────────────────────────────────

    [Fact]
    public void Decision_log_includes_reason_for_each_branch()
    {
        SpeedIndex index = MakeSpeedIndex(
            (VideoCodecType.H264, "libx264", 120),
            (VideoCodecType.H264, "h264_nvenc", 600)
        );

        // ForceSoftware
        ScopedDecisionLog fsLog = NewLog();
        _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceSoftware,
            WithNvenc(),
            index,
            fsLog
        );
        Assert.Contains("force_software", fsLog.Snapshot()[0].Message + fsLog.Snapshot()[0].Data);

        // PreferQuality
        ScopedDecisionLog pqLog = NewLog();
        _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferQuality,
            WithNvenc(),
            index,
            pqLog
        );
        Assert.Contains("PreferQuality", pqLog.Snapshot()[0].Message);

        // PreferHardware
        ScopedDecisionLog phLog = NewLog();
        _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.PreferHardware,
            WithNvenc(),
            index,
            phLog
        );
        Assert.Contains("PreferHardware", phLog.Snapshot()[0].Message);

        // ForceHardware
        ScopedDecisionLog fhLog = NewLog();
        _resolver.Resolve(
            VideoCodecType.H264,
            HardwarePreference.ForceHardware,
            WithNvenc(),
            index,
            fhLog
        );
        Assert.Contains("ForceHardware", fhLog.Snapshot()[0].Message);
    }
}
