namespace NoMercy.Tests.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

/// <summary>
/// Byte-for-byte snapshot tests for the HLS master playlist.
/// Each test fixes a codec variant → expected STREAM-INF line shape.
/// Bandwidth is zero (no measured metrics) so BANDWIDTH=0 is intentional —
/// this isolates codec string and attribute correctness from bitrate measurement.
/// </summary>
public class HlsMasterPlaylistSnapshotTests
{
    private const string MediaTitle = "TestTitle";

    private static readonly Dictionary<string, HlsVariantAnalyzer.VariantMetrics> EmptyVideo = [];
    private static readonly Dictionary<string, HlsVariantAnalyzer.VariantMetrics> EmptyAudio = [];

    private string Generate(OutputPlan plan)
    {
        PlaylistGenerator generator = new();
        return generator.GenerateMasterPlaylist(plan, MediaTitle, EmptyVideo, EmptyAudio);
    }

    // -----------------------------------------------------------------------
    // Header invariants
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_Header_BasicMpegTs_Version3()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "4.0", tenBit: false));

        Assert.StartsWith("#EXTM3U", m3u8);
        Assert.Contains("#EXT-X-VERSION:3", m3u8);
        Assert.Contains("#EXT-X-INDEPENDENT-SEGMENTS", m3u8);
    }

    [Fact]
    public void Snapshot_Header_Fmp4_Version7()
    {
        OutputPlan plan = H264Plan(
            "libx264",
            "high",
            "4.0",
            tenBit: false,
            hlsOptions: new NoMercy.Encoder.Profiles.HlsOptions { SegmentType = "fmp4" }
        );

        string m3u8 = Generate(plan);

        Assert.Contains("#EXT-X-VERSION:7", m3u8);
        Assert.Contains("#EXT-X-INDEPENDENT-SEGMENTS", m3u8);
    }

    [Fact]
    public void Snapshot_Header_WithSubs_Version6()
    {
        OutputPlan plan = H264PlanWithSubs("libx264", "high", "4.0");

        string m3u8 = Generate(plan);

        Assert.Contains("#EXT-X-VERSION:6", m3u8);
    }

    // -----------------------------------------------------------------------
    // H.264 codec strings
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_H264_High_Level40()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "4.0", tenBit: false));

        // High profile = 0x64, constraint = 0x00, level 40 = 0x28
        Assert.Contains("avc1.640028", m3u8);
    }

    [Fact]
    public void Snapshot_H264_High_Level41()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "4.1", tenBit: false));

        // level 41 = 0x29
        Assert.Contains("avc1.640029", m3u8);
    }

    [Fact]
    public void Snapshot_H264_High_Level50()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "5.0", tenBit: false));

        // level 50 = 0x32
        Assert.Contains("avc1.640032", m3u8);
    }

    [Fact]
    public void Snapshot_H264_High_Level51()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "5.1", tenBit: false));

        // level 51 = 0x33
        Assert.Contains("avc1.640033", m3u8);
    }

    [Fact]
    public void Snapshot_H264_Main_Level31()
    {
        string m3u8 = Generate(H264Plan("libx264", "main", "3.1", tenBit: false));

        // Main profile = 0x4D, constraint = 0x00, level 31 = 0x1F
        Assert.Contains("avc1.4D001F", m3u8);
    }

    [Fact]
    public void Snapshot_H264_Baseline_Level30()
    {
        string m3u8 = Generate(H264Plan("h264_nvenc", "baseline", "3.0", tenBit: false));

        // Baseline = 0x42, constraint = 0x40, level 30 = 0x1E
        Assert.Contains("avc1.42401E", m3u8);
    }

    // -----------------------------------------------------------------------
    // HEVC codec strings
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_Hevc_Sdr_Main_L93()
    {
        string m3u8 = Generate(H264Plan("hevc_nvenc", "main", "3.1", tenBit: false));

        // SDR Main: hvc1.1.6.L93.B0
        Assert.Contains("hvc1.1.6.L93.B0", m3u8);
    }

    [Fact]
    public void Snapshot_Hevc_Hdr10_Main10_L120()
    {
        string m3u8 = Generate(H264Plan("hevc_nvenc", "main10", "4.0", tenBit: true));

        // HDR Main10: hvc1.2.4.L120.B0
        Assert.Contains("hvc1.2.4.L120.B0", m3u8);
    }

    [Fact]
    public void Snapshot_Hevc_Sdr_L153()
    {
        string m3u8 = Generate(H264Plan("libx265", "main", "5.1", tenBit: false));

        // L5.1 = level_idc 153
        Assert.Contains("hvc1.1.6.L153.B0", m3u8);
    }

    // -----------------------------------------------------------------------
    // AV1 codec strings
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_Av1_8bit_Level40()
    {
        string m3u8 = Generate(H264Plan("libsvtav1", null, "4.0", tenBit: false));

        // Level 4.0 → index 08, 8-bit
        Assert.Contains("av01.0.08M.08", m3u8);
    }

    [Fact]
    public void Snapshot_Av1_10bit_Level50()
    {
        string m3u8 = Generate(H264Plan("libsvtav1", null, "5.0", tenBit: true));

        // Level 5.0 → index 12, 10-bit
        Assert.Contains("av01.0.12M.10", m3u8);
    }

    [Fact]
    public void Snapshot_Av1_8bit_Level51()
    {
        string m3u8 = Generate(H264Plan("libaom-av1", null, "5.1", tenBit: false));

        // Level 5.1 → index 13
        Assert.Contains("av01.0.13M.08", m3u8);
    }

    // -----------------------------------------------------------------------
    // Audio codec strings
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_Audio_Aac_Mp4a402()
    {
        string m3u8 = Generate(
            H264Plan("libx264", "high", "4.0", tenBit: false, audioCodec: "aac")
        );

        Assert.Contains("mp4a.40.2", m3u8);
    }

    [Fact]
    public void Snapshot_Audio_Ac3_Dash()
    {
        string m3u8 = Generate(
            H264Plan("libx264", "high", "4.0", tenBit: false, audioCodec: "ac3")
        );

        Assert.Contains("ac-3", m3u8);
    }

    [Fact]
    public void Snapshot_Audio_Eac3_Dash()
    {
        string m3u8 = Generate(
            H264Plan("libx264", "high", "4.0", tenBit: false, audioCodec: "eac3")
        );

        Assert.Contains("ec-3", m3u8);
    }

    // -----------------------------------------------------------------------
    // CLOSED-CAPTIONS=NONE when no subtitle group
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_ClosedCaptionsNone_WhenNoSubs()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "4.0", tenBit: false));

        Assert.Contains("CLOSED-CAPTIONS=NONE", m3u8);
        Assert.DoesNotContain("SUBTITLES=", m3u8);
    }

    [Fact]
    public void Snapshot_SubtitlesAttr_WhenSubsPresent()
    {
        string m3u8 = Generate(H264PlanWithSubs("libx264", "high", "4.0"));

        Assert.Contains("SUBTITLES=\"subs\"", m3u8);
        Assert.DoesNotContain("CLOSED-CAPTIONS=NONE", m3u8);
    }

    // -----------------------------------------------------------------------
    // VIDEO-RANGE
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_VideoRange_Sdr_When8Bit()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "4.0", tenBit: false));

        Assert.Contains("VIDEO-RANGE=SDR", m3u8);
    }

    [Fact]
    public void Snapshot_VideoRange_Pq_When10Bit()
    {
        string m3u8 = Generate(H264Plan("hevc_nvenc", "main10", "4.0", tenBit: true));

        Assert.Contains("VIDEO-RANGE=PQ", m3u8);
    }

    // -----------------------------------------------------------------------
    // FRAME-RATE
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_FrameRate_24()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "4.0", tenBit: false, frameRate: 24.0));

        Assert.Contains("FRAME-RATE=24.000", m3u8);
    }

    [Fact]
    public void Snapshot_FrameRate_2997()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "4.0", tenBit: false, frameRate: 29.97));

        Assert.Contains("FRAME-RATE=29.970", m3u8);
    }

    // -----------------------------------------------------------------------
    // Full STREAM-INF line snapshots
    // -----------------------------------------------------------------------

    [Fact]
    public void Snapshot_StreamInf_H264_High40_FullLine()
    {
        string m3u8 = Generate(H264Plan("libx264", "high", "4.0", tenBit: false));

        // Verify all mandatory attributes on one STREAM-INF line
        string? streamInfLine = m3u8.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(l => l.StartsWith("#EXT-X-STREAM-INF:"));

        Assert.NotNull(streamInfLine);
        Assert.Contains("BANDWIDTH=0", streamInfLine);
        Assert.Contains("AVERAGE-BANDWIDTH=0", streamInfLine);
        Assert.Contains("RESOLUTION=1920x1080", streamInfLine);
        Assert.Contains("FRAME-RATE=23.976", streamInfLine);
        Assert.Contains("CODECS=\"avc1.640028,mp4a.40.2\"", streamInfLine);
        Assert.Contains("VIDEO-RANGE=SDR", streamInfLine);
        Assert.Contains("CLOSED-CAPTIONS=NONE", streamInfLine);
    }

    [Fact]
    public void Snapshot_StreamInf_Hevc_Hdr10_FullLine()
    {
        string m3u8 = Generate(
            H264Plan("hevc_nvenc", "main10", "4.0", tenBit: true, audioCodec: "eac3")
        );

        string? streamInfLine = m3u8.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(l => l.StartsWith("#EXT-X-STREAM-INF:"));

        Assert.NotNull(streamInfLine);
        Assert.Contains("CODECS=\"hvc1.2.4.L120.B0,ec-3\"", streamInfLine);
        Assert.Contains("VIDEO-RANGE=PQ", streamInfLine);
    }

    // -----------------------------------------------------------------------
    // 3-variant ladder with subtitles — alignment plan item 722
    // -----------------------------------------------------------------------

    [Fact]
    public void MasterPlaylist_ThreeVariantLadderWithSubs_MatchesExpectedAttributes()
    {
        // Build a 3-video / 1-audio / 1-ASS-sub OutputPlan.
        // Metrics are empty (no encoded files on disk) so BANDWIDTH=0 / AVERAGE-BANDWIDTH=0
        // is intentional — this isolates attribute shape from measured bitrate.
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 854,
                    Height: 480,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "high",
                    Level: "3.1",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new(),
                    FrameRate: 30.0
                ),
                new(
                    Width: 1280,
                    Height: 720,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v1]",
                    ExtraFlags: new(),
                    FrameRate: 30.0
                ),
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: "libx264",
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v2]",
                    ExtraFlags: new(),
                    FrameRate: 30.0
                ),
            ],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 128,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "0:a:0"
                ),
            ],
            SubtitleOutputs:
            [
                new(
                    OutputCodec: SubtitleCodecType.Ass,
                    Action: StreamAction.Extract,
                    Language: "eng",
                    SourceIndex: 0,
                    MapLabel: "0:s:0"
                ),
            ],
            Thumbnails: null
        );

        PlaylistGenerator generator = new();
        string m3u8 = generator.GenerateMasterPlaylist(plan, MediaTitle, EmptyVideo, EmptyAudio);

        // --- Header ---
        Assert.StartsWith("#EXTM3U", m3u8);
        // Subtitle group present → must be at least v6
        Assert.Contains("#EXT-X-VERSION:6", m3u8);
        Assert.Contains("#EXT-X-INDEPENDENT-SEGMENTS", m3u8);

        // --- Subtitle EXT-X-MEDIA ---
        Assert.Contains(
            "#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID=\"subs\",NAME=\"English\",LANGUAGE=\"eng\"",
            m3u8
        );

        // --- Three STREAM-INF lines ---
        string[] streamInfLines = m3u8.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("#EXT-X-STREAM-INF:"))
            .ToArray();

        Assert.Equal(3, streamInfLines.Length);

        // Helper: assert all required attributes are present on every variant line.
        // BANDWIDTH / AVERAGE-BANDWIDTH are 0 because no encoded files exist (expected).
        foreach (string line in streamInfLines)
        {
            Assert.Contains("BANDWIDTH=0", line);
            Assert.Contains("AVERAGE-BANDWIDTH=0", line);
            Assert.Contains("FRAME-RATE=30.000", line);
            Assert.Contains("VIDEO-RANGE=SDR", line);
            Assert.Contains("SUBTITLES=\"subs\"", line);
            // CLOSED-CAPTIONS=NONE must NOT appear when a subs group is declared
            Assert.DoesNotContain("CLOSED-CAPTIONS=NONE", line);
            // CODECS must contain an h264 avc1 tag AND mp4a.40.2
            Assert.Contains("avc1.", line);
            Assert.Contains("mp4a.40.2", line);
        }

        // --- Per-variant RESOLUTION and codec tag ---
        string line480 = streamInfLines.First(l => l.Contains("RESOLUTION=854x480"));
        Assert.Contains("avc1.64001F", line480); // high/3.1 → 0x64 0x00 0x1F

        string line720 = streamInfLines.First(l => l.Contains("RESOLUTION=1280x720"));
        Assert.Contains("avc1.640028", line720); // high/4.0 → 0x64 0x00 0x28

        string line1080 = streamInfLines.First(l => l.Contains("RESOLUTION=1920x1080"));
        Assert.Contains("avc1.640028", line1080); // high/4.0 → 0x64 0x00 0x28

        // --- Variants appear in declaration order (480 → 720 → 1080) ---
        int idx480 = m3u8.IndexOf("RESOLUTION=854x480", StringComparison.Ordinal);
        int idx720 = m3u8.IndexOf("RESOLUTION=1280x720", StringComparison.Ordinal);
        int idx1080 = m3u8.IndexOf("RESOLUTION=1920x1080", StringComparison.Ordinal);

        Assert.True(idx480 < idx720, "480p variant must precede 720p variant");
        Assert.True(idx720 < idx1080, "720p variant must precede 1080p variant");
    }

    // -----------------------------------------------------------------------
    // Factories
    // -----------------------------------------------------------------------

    private static OutputPlan H264Plan(
        string encoderName,
        string? profile,
        string? level,
        bool tenBit,
        string audioCodec = "aac",
        double frameRate = 23.976,
        NoMercy.Encoder.Profiles.HlsOptions? hlsOptions = null
    )
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: encoderName,
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: profile,
                    Level: level,
                    TenBit: tenBit,
                    PixelFormat: tenBit ? "yuv420p10le" : "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new(),
                    FrameRate: frameRate
                ),
            ],
            AudioOutputs:
            [
                new(
                    EncoderName: audioCodec,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "0:a:0"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null,
            HlsOptions: hlsOptions
        );
    }

    private static OutputPlan H264PlanWithSubs(string encoderName, string? profile, string? level)
    {
        return new(
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Width: 1920,
                    Height: 1080,
                    EncoderName: encoderName,
                    Crf: 23,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: profile,
                    Level: level,
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v0]",
                    ExtraFlags: new()
                ),
            ],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
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
                    SourceIndex: 0,
                    MapLabel: "0:s:0"
                ),
            ],
            Thumbnails: null
        );
    }
}
