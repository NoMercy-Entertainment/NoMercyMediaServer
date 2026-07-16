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

using System.Security.Cryptography;
using System.Text;
using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// The presets every server ships with: shrink a library without giving up
/// quality, fan a title out to a full adaptive streaming set, or hand someone a
/// single file. Everything else is a user preset built on one of these through
/// ParentPresetId.
/// </summary>
public static class BuiltinPresets
{
    /// <summary>
    /// The preset a folder is linked to when auto-encode is switched on and
    /// nothing else is chosen. H.264 in MPEG-TS plays on every client NoMercy
    /// supports, so it is the safe default. <c>DefaultEncodingPresetLinker</c>
    /// resolves this name.
    /// </summary>
    public const string DefaultStreamingPresetName = "H.264 Streaming (Universal)";

    public static EncodingProfile[] All() =>
        [
            Remux(),
            Archive(
                "HEVC Archive (Visually Lossless)",
                "HEVC 10-bit at CRF 18, source resolution. Indistinguishable from the source at normal viewing distance and typically 40-60% smaller. Audio and subtitles are copied untouched.",
                VideoCodecType.H265,
                EncodingQuality.Ultra
            ),
            Archive(
                "HEVC Space Saver",
                "HEVC 10-bit at CRF 22, source resolution. The best size-to-quality trade for a large library. Audio and subtitles are copied untouched.",
                VideoCodecType.H265,
                EncodingQuality.High
            ),
            Archive(
                "AV1 Archive (Maximum Compression)",
                "AV1 10-bit at CRF 26, source resolution. Smallest files of any preset here and slow to produce, best for titles you rarely re-encode. Audio and subtitles are copied untouched.",
                VideoCodecType.Av1,
                EncodingQuality.VeryHigh
            ),
            SingleFileExport(
                "H.264 MP4 (Universal)",
                "H.264 MP4 with stereo AAC, source resolution. Plays anywhere.",
                VideoCodecType.H264,
                EncodingQuality.Balanced
            ),
            SingleFileExport(
                "HEVC MP4 (High Quality)",
                "HEVC 10-bit MP4 with stereo AAC, source resolution. Smaller than the H.264 export at the same quality, needs a reasonably modern player.",
                VideoCodecType.H265,
                EncodingQuality.High
            ),
            TwoPassExport(),
            LegacyBaseline(),
            StreamingLadder(
                DefaultStreamingPresetName,
                "Full H.264 ladder from 144p to 2160p in MPEG-TS. The widest-reaching option and the default for auto-encode.",
                VideoCodecType.H264,
                LadderTiers.YouTube,
                Container.HlsTs,
                [AacStereo()]
            ),
            StreamingLadder(
                "HEVC Streaming (Premium)",
                "HEVC ladder from 360p to 2160p in fMP4, with stereo AAC and 5.1 E-AC-3. Roughly half the bitrate of H.264 for the same picture.",
                VideoCodecType.H265,
                LadderTiers.Premium,
                Container.HlsFmp4,
                [AacStereo(), Eac3Surround()]
            ),
            HdrStreamingLadder(),
            TonemappedStreamingLadder(),
            StreamingLadder(
                "AV1 Streaming (Efficient)",
                "AV1 ladder from 360p to 2160p in fMP4. The cheapest bitrate per rung, for clients new enough to decode it.",
                VideoCodecType.Av1,
                LadderTiers.Premium,
                Container.HlsFmp4,
                [AacStereo()]
            ),
            StreamingLadder(
                "DASH Streaming (Universal)",
                "H.264 ladder from 360p to 2160p in DASH, for clients that speak MPEG-DASH rather than HLS.",
                VideoCodecType.H264,
                LadderTiers.Premium,
                Container.Dash,
                [AacStereo()]
            ),
            DirectStreamAudio(),
            // Fixed-resolution library presets. The ladders above adapt per source;
            // these pin an output for a library where every title should land on the
            // same rung. Height clamps to the source, so a lower-resolution episode
            // is never upscaled to meet the name.
            LibraryPreset(
                "Anime 1080p HEVC 10-bit",
                "HEVC 10-bit 1080p tuned for animation: flat colour and hard line art get the bitrate that x265's default tuning spends on film grain. CRF 18.",
                width: 1920,
                height: 1080,
                quality: EncodingQuality.Ultra,
                tune: "animation"
            ),
            LibraryPreset(
                "1080p HEVC 10-bit",
                "HEVC 10-bit 1080p at CRF 18. Visually lossless against a 1080p source, and the smallest a 1080p library gets without giving up picture.",
                width: 1920,
                height: 1080,
                quality: EncodingQuality.Ultra
            ),
            LibraryPreset(
                "4K HDR HEVC 10-bit",
                "HEVC 10-bit 2160p with HDR carried through untouched. CRF 18, so the only thing that changes against a 4K HDR source is the codec.",
                width: 3840,
                height: 2160,
                quality: EncodingQuality.Ultra,
                hdrPolicy: HdrPolicies.AlwaysPreserve,
                hdrOptions: new() { Force10Bit = true }
            ),
            LibraryPreset(
                "1080p SDR HEVC 10-bit",
                "HEVC 10-bit 1080p with any HDR source tonemapped down to SDR. The companion to the 4K HDR preset: pair them on a folder so HDR clients take the 4K and everything else gets a correct SDR picture rather than a washed-out one.",
                width: 1920,
                height: 1080,
                quality: EncodingQuality.Ultra,
                hdrPolicy: HdrPolicies.AlwaysTonemap,
                convertHdrToSdr: true
            ),
            MusicTrack(
                "Music FLAC Lossless",
                "Lossless FLAC, stereo, 48 kHz.",
                Container.Flac,
                AudioCodecType.Flac,
                bitrateKbps: 0,
                sampleRateHz: 48000
            ),
            MusicTrack(
                "Music AAC 256k",
                "AAC 256 kbps, stereo, 44.1 kHz.",
                Container.Aac,
                AudioCodecType.Aac,
                bitrateKbps: 256,
                sampleRateHz: 44100
            ),
            MusicTrack(
                "Music MP3 320k",
                "MP3 320 kbps, stereo, 44.1 kHz.",
                Container.Mp3,
                AudioCodecType.Mp3,
                bitrateKbps: 320,
                sampleRateHz: 44100
            ),
        ];

    /// <summary>
    /// Built-in ids are derived from the name so they stay identical across
    /// installs. Renaming a preset therefore mints a new id: pair the old and
    /// new id in <see cref="BuiltinPresetRenames"/> or existing folder links are
    /// retired rather than carried over.
    /// </summary>
    private static Ulid IdFromName(string name)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return new(hash.AsSpan(0, 16).ToArray());
    }

    private static AudioOutput AudioTrack(
        AudioCodecType codec,
        int bitrateKbps,
        int sampleRateHz,
        int channels
    ) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            BitrateKbps: bitrateKbps,
            Channels: channels,
            SampleRateHz: sampleRateHz,
            AllowedLanguages: AllowedLanguages.All,
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}",
            PlaylistNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}"
        );

    private static AudioOutput AacStereo() => AudioTrack(AudioCodecType.Aac, 192, 48000, 2);

    private static AudioOutput Eac3Surround() => AudioTrack(AudioCodecType.Eac3, 448, 48000, 6);

    private static AudioOutput CopyAudio() =>
        new(
            Policy: StreamPolicy.Copy,
            Codec: AudioCodecType.Copy,
            BitrateKbps: 0,
            Channels: 0,
            SampleRateHz: 0,
            AllowedLanguages: AllowedLanguages.All,
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}",
            PlaylistNameTemplate: "audio_{lang}_{codec}/audio_{lang}_{codec}"
        );

    private static SubtitleOutput DefaultSubs() =>
        new(
            Policy: SubtitlePolicy.Extract,
            Codec: SubtitleCodecType.WebVtt,
            AllowedLanguages: AllowedLanguages.All,
            IncludeForced: true,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/{filename}.{lang}.{type}"
        );

    private static SubtitleOutput CopySubs() =>
        new(
            Policy: SubtitlePolicy.Copy,
            Codec: SubtitleCodecType.Copy,
            AllowedLanguages: AllowedLanguages.All,
            IncludeForced: true,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/{filename}.{lang}.{type}"
        );

    /// <summary>
    /// CRF video. A null width and height mean "keep the source", which is what
    /// an archive or export preset wants: rescaling is quality loss, and the
    /// ladder expander rescales per rung from this reference anyway.
    /// </summary>
    private static VideoOutput VideoCrf(
        VideoCodecType codec,
        CodecTunings.CodecTuning tuning,
        int? width = null,
        int? height = null,
        CodecProfile? codecProfile = null,
        string? level = null,
        string? tune = null,
        bool convertHdrToSdr = false,
        int? bitDepth = null
    ) =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: codec,
            Width: width,
            Height: height,
            RateControl: RateControlMode.Crf,
            Crf: tuning.Crf,
            BitrateKbps: 0,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: tuning.Preset,
            CodecProfile: codecProfile ?? tuning.Profile,
            Level: level,
            Tune: tune,
            BitDepth: bitDepth ?? tuning.BitDepth,
            PixelFormat: (bitDepth ?? tuning.BitDepth) == 10 ? "yuv420p10le" : "yuv420p",
            KeyframeIntervalSeconds: 4,
            ConvertHdrToSdr: convertHdrToSdr,
            SegmentNameTemplate: "video_{label}/video_{label}",
            PlaylistNameTemplate: "video_{label}/video_{label}",
            CustomArguments: tuning.ExtraArgs
        );

    private static VideoOutput CopyVideo() =>
        new(
            Policy: StreamPolicy.Copy,
            Codec: VideoCodecType.Copy,
            Width: null,
            Height: null,
            RateControl: RateControlMode.Crf,
            Crf: 0,
            BitrateKbps: 0,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: null,
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 4,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video/copy",
            PlaylistNameTemplate: "video/copy/playlist"
        );

    private static EncodingProfile SingleFile(
        string name,
        Container container,
        VideoOutput? video,
        AudioOutput[] audio,
        SubtitleOutput[] subs,
        string description,
        HardwarePreference hw = HardwarePreference.PreferHardware,
        ClientCompatibility compat = ClientCompatibility.Universal,
        EncodeMode encodeMode = EncodeMode.SinglePass
    ) =>
        new(
            Id: IdFromName(name),
            Name: name,
            Container: container,
            Video: video,
            Audio: audio,
            Subtitles: subs,
            Thumbnails: null,
            Ladder: null,
            Hls: null,
            HlsDerivatives: null,
            Dash: null,
            Drm: null,
            EncodeMode: encodeMode
        )
        {
            Description = description,
            IsBuiltin = true,
            HardwarePreference = hw,
            ClientCompatibility = compat,
        };

    private static EncodingProfile Remux() =>
        SingleFile(
            name: "Remux (Lossless MKV)",
            container: Container.Mkv,
            video: CopyVideo(),
            audio: [CopyAudio()],
            subs: [CopySubs()],
            description: "Copies every track straight into MKV. Nothing is re-encoded, so this only fixes the container and costs a disk pass.",
            hw: HardwarePreference.PreferQuality
        );

    /// <summary>
    /// Re-encodes video only. Audio and subtitles are stream-copied, which keeps
    /// surround tracks and image subtitles (PGS) exactly as they were: an archive
    /// preset is meant to lose nothing but bitrate.
    /// </summary>
    private static EncodingProfile Archive(
        string name,
        string description,
        VideoCodecType codec,
        EncodingQuality quality
    ) =>
        SingleFile(
            name: name,
            container: Container.Mkv,
            video: VideoCrf(codec, CodecTunings.For(codec, quality)),
            audio: [CopyAudio()],
            subs: [CopySubs()],
            description: description,
            // Hardware encoders trade quality for bitrate, and an archive encode
            // is written once and kept. Spend the CPU.
            hw: HardwarePreference.PreferQuality
        );

    private static EncodingProfile SingleFileExport(
        string name,
        string description,
        VideoCodecType codec,
        EncodingQuality quality
    ) =>
        SingleFile(
            name: name,
            container: Container.Mp4,
            video: VideoCrf(codec, CodecTunings.For(codec, quality)),
            audio: [AacStereo()],
            subs: [DefaultSubs()],
            description: description
        );

    /// <summary>
    /// Two-pass only pays off against a bitrate target, so this pins both the
    /// rate control and the resolution: a target bitrate is meaningless without
    /// a known frame size. CRF stays 0 or the rate-control rules reject the
    /// profile for naming two competing targets.
    /// </summary>
    private static EncodingProfile TwoPassExport()
    {
        CodecTunings.CodecTuning tuning = CodecTunings.For(
            VideoCodecType.H264,
            EncodingQuality.High
        );

        VideoOutput video = new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Vbr,
            Crf: 0,
            BitrateKbps: 5000,
            MaxBitrateKbps: 7500,
            BufferSizeKbps: 10000,
            Preset: tuning.Preset,
            CodecProfile: CodecProfile.High,
            Level: "4.0",
            Tune: null,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            KeyframeIntervalSeconds: 4,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video_{label}/video_{label}",
            PlaylistNameTemplate: "video_{label}/video_{label}"
        );

        return SingleFile(
            name: "H.264 MP4 1080p (2-Pass 5 Mbps)",
            container: Container.Mp4,
            video: video,
            audio: [AacStereo()],
            subs: [DefaultSubs()],
            description: "H.264 1080p MP4 at a measured 5 Mbps using two passes. Pick this when the file has to land on a predictable size; the CRF presets give better quality per bit when size is not fixed.",
            hw: HardwarePreference.PreferQuality,
            encodeMode: EncodeMode.TwoPass
        );
    }

    private static EncodingProfile LegacyBaseline() =>
        SingleFile(
            name: "Legacy Device H.264 Baseline",
            container: Container.Mp4,
            video: VideoCrf(
                VideoCodecType.H264,
                CodecTunings.For(VideoCodecType.H264, EncodingQuality.Fast),
                codecProfile: CodecProfile.Baseline,
                level: "4.0",
                bitDepth: 8
            ),
            audio: [AacStereo()],
            subs: [DefaultSubs()],
            description: "H.264 Baseline MP4 for hardware that chokes on anything else. Larger than the universal export at the same quality, and the last resort rather than a default.",
            compat: ClientCompatibility.Universal | ClientCompatibility.LegacyDevices
        );

    /// <summary>
    /// Audio-only HLS that stream-copies the source. For music libraries served
    /// over HLS where re-encoding buys nothing.
    /// </summary>
    private static EncodingProfile DirectStreamAudio() =>
        new(
            Id: IdFromName("Direct Stream Audio HLS"),
            Name: "Direct Stream Audio HLS",
            Container: Container.AudioHlsFmp4,
            Video: null,
            Audio: [CopyAudio()],
            Subtitles: [],
            Thumbnails: null,
            Ladder: null,
            Hls: new(),
            HlsDerivatives: new()
            {
                GenerateSpriteVtt = false,
                GenerateThumbnailTrack = false,
                GenerateFontsJson = false,
            },
            Dash: null,
            Drm: null
        )
        {
            Description =
                "Segments the source audio into fMP4 HLS without re-encoding it. Nothing is transcoded, so the only cost is the segmenting pass.",
            IsBuiltin = true,
            HardwarePreference = HardwarePreference.PreferQuality,
            ClientCompatibility = ClientCompatibility.Universal,
        };

    private static EncodingProfile MusicTrack(
        string name,
        string description,
        Container container,
        AudioCodecType codec,
        int bitrateKbps,
        int sampleRateHz
    ) =>
        SingleFile(
            name: name,
            container: container,
            video: null,
            audio: [AudioTrack(codec, bitrateKbps, sampleRateHz, 2)],
            subs: [],
            description: description
        );

    /// <summary>
    /// HDR preserved on the 10-bit rungs, tonemapped SDR on the 8-bit ones.
    /// PlanStage reads EmitHdrAndSdr and splits ConvertHdrToSdr by bit depth.
    /// </summary>
    private static EncodingProfile HdrStreamingLadder() =>
        StreamingLadder(
            "HEVC HDR Streaming (Premium)",
            "The HEVC premium ladder with HDR preserved end to end. Clients that cannot take HDR pull the tonemapped SDR rungs instead.",
            VideoCodecType.H265,
            LadderTiers.Premium,
            Container.HlsFmp4,
            [AacStereo(), Eac3Surround()],
            hdrPolicy: HdrPolicies.EmitHdrAndSdr,
            hdrOptions: new() { Force10Bit = true }
        );

    /// <summary>
    /// Converts HDR sources to SDR outright. AlwaysTonemap only stops the
    /// tonemap from being suppressed and strips Dolby Vision; PlanStage does not
    /// set ConvertHdrToSdr for this policy, so the reference output has to ask
    /// for the conversion itself or the flag never reaches the filter chain.
    /// </summary>
    private static EncodingProfile TonemappedStreamingLadder() =>
        StreamingLadder(
            "H.264 Streaming (HDR to SDR)",
            "Tonemaps an HDR source down to 8-bit SDR H.264. For libraries whose clients cannot render HDR at all, where the passthrough ladders would hand them washed-out picture.",
            VideoCodecType.H264,
            LadderTiers.YouTube,
            Container.HlsTs,
            [AacStereo()],
            hdrPolicy: HdrPolicies.AlwaysTonemap,
            convertHdrToSdr: true
        );

    /// <summary>
    /// A single-rendition HLS output at a fixed resolution, for a library where
    /// every title should land on the same rung rather than fan out into a
    /// ladder. HEVC 10-bit throughout: 10-bit costs nothing on an 8-bit source
    /// and removes the banding that 8-bit HEVC introduces in gradients.
    /// </summary>
    private static EncodingProfile LibraryPreset(
        string name,
        string description,
        int width,
        int height,
        EncodingQuality quality,
        string? tune = null,
        HdrPolicies hdrPolicy = HdrPolicies.PassthroughWhenPossible,
        HdrOptions? hdrOptions = null,
        bool convertHdrToSdr = false
    )
    {
        CodecTunings.CodecTuning tuning = CodecTunings.For(VideoCodecType.H265, quality);

        return new(
            Id: IdFromName(name),
            Name: name,
            Container: Container.HlsFmp4,
            Video: VideoCrf(
                VideoCodecType.H265,
                tuning,
                width: width,
                height: height,
                tune: tune,
                convertHdrToSdr: convertHdrToSdr
            ),
            Audio: [AacStereo(), Eac3Surround()],
            Subtitles: [DefaultSubs()],
            Thumbnails: null,
            Ladder: null,
            Hls: new(),
            HlsDerivatives: new(),
            Dash: null,
            Drm: null
        )
        {
            Description = description,
            IsBuiltin = true,
            ClientCompatibility = ClientCompatibility.Universal,
            HardwarePreference = HardwarePreference.PreferQuality,
            BitDepthPolicy = BitDepthPolicy.WarnAndDowngrade,
            HdrPolicies = hdrPolicy,
            HdrOptions = hdrOptions,
        };
    }

    /// <summary>
    /// An adaptive ladder. The codec and quality build the reference
    /// <see cref="VideoOutput"/> that AutoLadderExpander rescales per rung:
    /// with no reference it logs and emits no video at all, so Video must never
    /// be null here.
    /// </summary>
    private static EncodingProfile StreamingLadder(
        string name,
        string description,
        VideoCodecType codec,
        LadderTier[] tiers,
        Container container,
        AudioOutput[] audio,
        HdrPolicies hdrPolicy = HdrPolicies.PassthroughWhenPossible,
        HdrOptions? hdrOptions = null,
        bool convertHdrToSdr = false,
        ClientCompatibility compat = ClientCompatibility.Universal
    )
    {
        CodecTunings.CodecTuning tuning = CodecTunings.For(codec, EncodingQuality.Streaming);

        AutoLadderConfig autoConfig = new()
        {
            Tiers = tiers,
            BitrateStrategy = BitrateStrategy.AppleHlsRecommended,
            CodecPolicy = LadderCodecPolicy.Uniform,
            Crf = tuning.Crf,
            MaxRungs = Math.Max(1, tiers.Length),
            MinRungs = 1,
            NeverUpscale = true,
            VbrCeilingMultiplier = 1.5,
            BufferSizeMultiplier = 2.0,
        };

        return new(
            Id: IdFromName(name),
            Name: name,
            Container: container,
            Video: VideoCrf(codec, tuning, convertHdrToSdr: convertHdrToSdr),
            Audio: audio,
            Subtitles: [DefaultSubs()],
            Thumbnails: null,
            Ladder: new() { Mode = LadderMode.Auto, AutoConfig = autoConfig },
            Hls: container is Container.HlsTs or Container.HlsFmp4 ? new() : null,
            HlsDerivatives: container is Container.HlsTs or Container.HlsFmp4 ? new() : null,
            Dash: null,
            Drm: null
        )
        {
            Description = description,
            IsBuiltin = true,
            ClientCompatibility = compat,
            HardwarePreference = HardwarePreference.PreferHardware,
            BitDepthPolicy = BitDepthPolicy.WarnAndDowngrade,
            HdrPolicies = hdrPolicy,
            HdrOptions = hdrOptions,
        };
    }
}
