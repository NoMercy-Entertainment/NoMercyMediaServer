using System.Security.Cryptography;
using System.Text;
using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Factory for the 12 built-in encoding presets that ship with NoMercy.
/// Each preset has a deterministic, stable Id derived from its name so
/// re-seeding across server upgrades never changes existing row Ids.
/// </summary>
public static class BuiltinPresets
{
    // ──────────────────────────────────────────────────────────────────────
    // Shared subtitle output used by all video presets
    // ──────────────────────────────────────────────────────────────────────

    private static SubtitleOutput DefaultSubtitles() =>
        new(Codec: SubtitleCodecType.WebVtt, Mode: SubtitleMode.Extract, AllowedLanguages: [])
        {
            IncludeForced = true,
        };

    // ──────────────────────────────────────────────────────────────────────
    // Deterministic Ulid — SHA-256(name)[0..15] → 16-byte Ulid payload
    // ──────────────────────────────────────────────────────────────────────

    private static Ulid BuiltinPresetId(string name)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        // Ulid is 128 bits: 48-bit timestamp + 80-bit random.
        // We overwrite the full 16 bytes with the hash so the Id is
        // deterministic. The Ulid(ReadOnlySpan<byte>) ctor reads exactly
        // 16 bytes.
        return new Ulid(hash.AsSpan(0, 16));
    }

    // ──────────────────────────────────────────────────────────────────────
    // 1. General H.264 1080p (fast)
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile GeneralH264_1080p_Fast() =>
        new(
            Id: BuiltinPresetId("GeneralH264_1080p_Fast"),
            Name: "General H.264 1080p — Fast",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H264,
                    Width: 1920,
                    Height: 1080,
                    BitrateKbps: 0,
                    Crf: 23,
                    Preset: "fast",
                    Profile: "high",
                    Level: "4.1",
                    ConvertHdrToSdr: true,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [DefaultSubtitles()],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "Broad-compatibility H.264 HLS stream at 1080p. Fast encode preset — good for NAS / low-power hardware.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 2. Web H.264 720p
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Web_H264_720p() =>
        new(
            Id: BuiltinPresetId("Web_H264_720p"),
            Name: "Web H.264 720p",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H264,
                    Width: 1280,
                    Height: 720,
                    BitrateKbps: 0,
                    Crf: 23,
                    Preset: "medium",
                    Profile: "high",
                    Level: "3.1",
                    ConvertHdrToSdr: true,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 128,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [DefaultSubtitles()],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "Lightweight H.264 HLS stream at 720p — ideal for browser playback and lower-bandwidth connections.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 3. Archival HEVC 1080p
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Archival_HEVC_1080p() =>
        new(
            Id: BuiltinPresetId("Archival_HEVC_1080p"),
            Name: "Archival HEVC 1080p",
            Format: OutputFormat.Mkv,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H265,
                    Width: 1920,
                    Height: 1080,
                    BitrateKbps: 0,
                    Crf: 20,
                    Preset: "slow",
                    Profile: "main",
                    Level: "4.0",
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Flac,
                    BitrateKbps: 0,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [DefaultSubtitles()],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "High-quality HEVC archival encode to MKV at 1080p with lossless FLAC audio — optimised for long-term storage.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 4. Anime H.264 1080p
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Anime_H264_1080p() =>
        new(
            Id: BuiltinPresetId("Anime_H264_1080p"),
            Name: "Anime H.264 1080p",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H264,
                    Width: 1920,
                    Height: 1080,
                    BitrateKbps: 0,
                    Crf: 20,
                    Preset: "slow",
                    Profile: "high",
                    Level: "4.1",
                    ConvertHdrToSdr: true,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false,
                    Tune: "animation"
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [DefaultSubtitles()],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "H.264 HLS at 1080p tuned for animation — slower preset and animation tune reduce banding on flat-colour scenes.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 5. Music AAC 192k
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Music_AAC_192k() =>
        new(
            Id: BuiltinPresetId("Music_AAC_192k"),
            Name: "Music AAC 192k",
            // MP4 (M4A) is the native container for AAC. Ogg only accepts
            // Vorbis/Opus/FLAC — using Ogg with AAC would fail validation.
            Format: OutputFormat.Mp4,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 44100,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "AAC stereo at 192 kbps in MP4/M4A — transparent quality for most listeners with broad device support.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 6. Music MP3 320k
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Music_MP3_320k() =>
        new(
            Id: BuiltinPresetId("Music_MP3_320k"),
            Name: "Music MP3 320k",
            Format: OutputFormat.Mp3,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Mp3,
                    BitrateKbps: 320,
                    Channels: 2,
                    SampleRateHz: 44100,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "MP3 stereo at 320 kbps — maximum MP3 bitrate for legacy-device compatibility.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 7. Music FLAC Lossless
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Music_FLAC_Lossless() =>
        new(
            Id: BuiltinPresetId("Music_FLAC_Lossless"),
            Name: "Music FLAC Lossless",
            Format: OutputFormat.Flac,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Flac,
                    BitrateKbps: 0,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "Lossless FLAC archival — bit-perfect audio preservation for music libraries.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 8. Chromecast H.264 1080p
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Chromecast_H264_1080p() =>
        new(
            Id: BuiltinPresetId("Chromecast_H264_1080p"),
            Name: "Chromecast H.264 1080p",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H264,
                    Width: 1920,
                    Height: 1080,
                    BitrateKbps: 0,
                    Crf: 23,
                    Preset: "fast",
                    Profile: "high",
                    Level: "4.1",
                    ConvertHdrToSdr: true,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
                new(
                    Codec: AudioCodecType.Ac3,
                    BitrateKbps: 384,
                    Channels: 6,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [DefaultSubtitles()],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "H.264 HLS at 1080p optimised for Chromecast — AAC stereo + AC3 5.1 surround, within Chromecast Ultra playback limits.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 9. Apple TV HEVC 4K Main10 (fmp4 segments)
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile AppleTV_HEVC_4K_Main10() =>
        new(
            Id: BuiltinPresetId("AppleTV_HEVC_4K_Main10"),
            Name: "Apple TV HEVC 4K Main10",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H265,
                    Width: 3840,
                    Height: 2160,
                    BitrateKbps: 0,
                    Crf: 20,
                    Preset: "slow",
                    Profile: "main10",
                    Level: "5.1",
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: true
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 256,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
                new(
                    Codec: AudioCodecType.Eac3,
                    BitrateKbps: 448,
                    Channels: 6,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [DefaultSubtitles()],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "HEVC Main10 HLS at 4K with fmp4 segments — tailored for Apple TV 4K HDR playback with EAC3 Dolby Digital Plus surround.",
            // fmp4 segments allow HEVC + HDR in HLS on Apple platforms.
            // The HlsOptions field (SegmentType = "fmp4") is expressed here via
            // CustomArguments as a profile-level hint; the HLS strategy reads
            // this key when building the ffmpeg -hls_segment_type flag.
            CustomArguments = new Dictionary<string, string> { ["hls_segment_type"] = "fmp4" },
        };

    // ──────────────────────────────────────────────────────────────────────
    // 10. Mobile H.264 480p Low-Bandwidth
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Mobile_H264_480p_LowBandwidth() =>
        new(
            Id: BuiltinPresetId("Mobile_H264_480p_LowBandwidth"),
            Name: "Mobile H.264 480p — Low Bandwidth",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H264,
                    Width: 854,
                    Height: 480,
                    BitrateKbps: 0,
                    Crf: 26,
                    Preset: "fast",
                    Profile: "main",
                    Level: "3.1",
                    ConvertHdrToSdr: true,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 96,
                    Channels: 2,
                    SampleRateHz: 44100,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [DefaultSubtitles()],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "Low-bandwidth H.264 HLS at 480p — designed for mobile data connections and older hardware.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 11. Archival HEVC 4K Main10 (two-pass)
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Archival_HEVC_4K_Main10() =>
        // TwoPass is used here for 4K archival to give the encoder a full
        // first-pass bitrate analysis — especially valuable for long-form
        // content where per-scene complexity varies widely.
        new(
            Id: BuiltinPresetId("Archival_HEVC_4K_Main10"),
            Name: "Archival HEVC 4K Main10",
            Format: OutputFormat.Mkv,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H265,
                    Width: 3840,
                    Height: 2160,
                    BitrateKbps: 0,
                    Crf: 18,
                    Preset: "slow",
                    Profile: "main10",
                    Level: "5.1",
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: true
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Flac,
                    BitrateKbps: 0,
                    Channels: 6,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [DefaultSubtitles()],
            SegmentDurationSeconds: 6,
            // Two-pass gives accurate bitrate distribution across a 4K
            // feature film. CRF still controls quality — the two passes
            // analyse then encode rather than targeting a specific bitrate.
            EncodeMode: EncodeMode.TwoPass
        )
        {
            IsBuiltin = true,
            Description =
                "Lossless-quality 4K HEVC Main10 archival to MKV with FLAC 5.1 audio — two-pass for optimal bitrate distribution.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // 12. Anime HEVC 1080p 10-bit
    // ──────────────────────────────────────────────────────────────────────

    public static EncodingProfile Anime_HEVC_1080p_10bit() =>
        new(
            Id: BuiltinPresetId("Anime_HEVC_1080p_10bit"),
            Name: "Anime HEVC 1080p 10-bit",
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H265,
                    Width: 1920,
                    Height: 1080,
                    BitrateKbps: 0,
                    Crf: 20,
                    Preset: "slow",
                    Profile: "main10",
                    Level: "4.1",
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: true,
                    Tune: "animation"
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: [DefaultSubtitles()],
            SegmentDurationSeconds: 6
        )
        {
            IsBuiltin = true,
            Description =
                "HEVC Main10 HLS at 1080p tuned for animation — 10-bit depth eliminates banding on gradient skies and flat-colour anime scenes.",
        };

    // ──────────────────────────────────────────────────────────────────────
    // All()
    // ──────────────────────────────────────────────────────────────────────

    public static IReadOnlyList<EncodingProfile> All() =>
        [
            GeneralH264_1080p_Fast(),
            Web_H264_720p(),
            Archival_HEVC_1080p(),
            Anime_H264_1080p(),
            Music_AAC_192k(),
            Music_MP3_320k(),
            Music_FLAC_Lossless(),
            Chromecast_H264_1080p(),
            AppleTV_HEVC_4K_Main10(),
            Mobile_H264_480p_LowBandwidth(),
            Archival_HEVC_4K_Main10(),
            Anime_HEVC_1080p_10bit(),
        ];
}
