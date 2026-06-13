using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;
using DrmConfig = NoMercy.Encoder.BuildingBlocks.Drm.DrmConfig;

namespace NoMercy.Encoder.Output;

public record OutputPlan(
    OutputFormat Format,
    VideoOutputPlan[] VideoOutputs,
    AudioOutputPlan[] AudioOutputs,
    SubtitleOutputPlan[] SubtitleOutputs,
    ThumbnailOutputPlan? Thumbnails,
    int SegmentDurationSeconds = 6,
    // Set by PlanStage when the source carries Dolby Vision metadata AND the
    // video output is stream-copy. Output strategies use this to emit the
    // container-specific codec tag that preserves DV playback signaling
    // (dvh1 for MP4/HLS, hvc1+dv passthrough for MKV).
    bool PreserveDolbyVision = false,
    // DRM config propagated from the profile. BuildStage runs the matching
    // IDrmProcessor to generate key + keyinfo artifacts, then injects the
    // resulting `-hls_key_info_file` path into each video output's extra
    // flags so ffmpeg encrypts segments inline and emits EXT-X-KEY tags
    // in the playlist automatically.
    DrmConfig? Drm = null,
    // HLS muxer options forwarded from the profile. Controls playlist type,
    // segment container format, and independent-segments signaling.
    HlsPlanOptions? HlsOptions = null,
    // Chapter metadata from the source file. Output strategies use this to
    // embed chapter data in container-appropriate format:
    //   MKV  — stream-copied by FFmpeg automatically; no extra args needed.
    //   MP4  — written as ffmetadata file, injected via -i chapters.ffmeta.
    //   DASH — post-processed into <EventStream> entries in the MPD.
    //   HLS  — emitted as #EXT-X-DATERANGE tags in the master playlist.
    IReadOnlyList<ChapterInfo>? Chapters = null,
    // Resolved by PlanStage when EncodingContext.MediaItem is set.
    // Provides the canonical bundle directory + per-output filenames.
    BundleLayout? Layout = null,
    // Subtitles downloaded by SubtitleAcquisitionService during PlanStage.
    // BuildStage adds exact-match entries as FFmpeg -i inputs.
    IReadOnlyList<AcquiredSubtitle>? AcquiredSubtitles = null,
    // Opt-in: when true the decomposer emits one Chapters task per chapter
    // and BuildStage extracts a single still at the chapter's exact timestamp.
    bool GenerateChapterThumbs = false,
    // When true, HlsOutputStrategy slices each extracted WebVTT subtitle into
    // HLS segments + per-track media playlist (subtitles/{lang}/{variant}.m3u8)
    // and the master playlist references those m3u8 wrappers. When false the
    // raw .vtt extract still lands on disk for download but no segments are
    // produced and the master playlist omits the EXT-X-MEDIA subtitle entry.
    bool EmitSubtitleWebVttChunks = true,
    // Set by PlanStage when the plan is GPU-resident-eligible and a GPU vendor +
    // scaler are available. BuildStage then decodes with -hwaccel into GPU memory
    // and scales on the GPU (scale_cuda / scale_qsv) so decode + scaling leave
    // the CPU. Null = the CPU filter graph (default, unchanged behaviour).
    GpuAccelPlan? GpuAccel = null
);

public record VideoOutputPlan(
    int Width,
    int Height,
    string EncoderName,
    int Crf,
    int BitrateKbps,
    string? Preset,
    string? Profile,
    string? Level,
    bool TenBit,
    string PixelFormat,
    string MapLabel,
    Dictionary<string, string> ExtraFlags,
    double FrameRate = 23.976,
    string SegmentNameTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
    string PlaylistNameTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
    bool ConvertHdrToSdr = false,
    string? TonemapFilterChain = null,
    string? CropFilter = null,
    // True when this output preserves the source's HDR transfer characteristics
    // (PQ / HLG / SMPTE2084). Used by the :colorrange: template token so folder
    // and playlist names label outputs HDR vs SDR based on the actual color
    // pipeline, not bit depth — 10-bit BT.709 is SDR and conflating depth with
    // HDR was mislabeling every 10-bit anime / SDR remux as HDR.
    bool IsHdrOutput = false
);

public record AudioOutputPlan(
    string EncoderName,
    int BitrateKbps,
    int Channels,
    int SampleRate,
    StreamAction Action,
    string? Language,
    string MapLabel,
    string SegmentNameTemplate = ":type:_:language:_:codec:/:type:_:language:_:codec:",
    string PlaylistNameTemplate = ":type:_:language:_:codec:/:type:_:language:_:codec:",
    string? AudioFilter = null
);

public record SubtitleOutputPlan(
    SubtitleCodecType OutputCodec,
    StreamAction Action,
    string? Language,
    int SourceIndex,
    string? MapLabel,
    string PlaylistNameTemplate = "subtitles/:filename:.:language:.:variant:",
    SubtitlePolicy Policy = SubtitlePolicy.Extract,
    // Variant slot — full / sign / sdh / alt — derived from the source stream's
    // title and disposition flags so multi-track sources keep distinct URIs.
    string Variant = "full"
);

public record ThumbnailOutputPlan(int Width, int Height, int IntervalSeconds);
