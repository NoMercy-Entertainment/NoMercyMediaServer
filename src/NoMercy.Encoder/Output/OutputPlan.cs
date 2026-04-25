namespace NoMercy.Encoder.Output;

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

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
    NoMercy.Encoder.BuildingBlocks.Drm.DrmConfig? Drm = null,
    // HLS muxer options forwarded from the profile. Controls playlist type,
    // segment container format, and independent-segments signaling.
    NoMercy.Encoder.Profiles.HlsOptions? HlsOptions = null,
    // Chapter metadata from the source file. Output strategies use this to
    // embed chapter data in container-appropriate format:
    //   MKV  — stream-copied by FFmpeg automatically; no extra args needed.
    //   MP4  — written as ffmetadata file, injected via -i chapters.ffmeta.
    //   DASH — post-processed into <EventStream> entries in the MPD.
    //   HLS  — emitted as #EXT-X-DATERANGE tags in the master playlist.
    IReadOnlyList<NoMercy.Encoder.Analysis.ChapterInfo>? Chapters = null
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
    string? CropFilter = null
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
    SubtitleMode Mode = SubtitleMode.Extract
);

public record ThumbnailOutputPlan(int Width, int Height, int IntervalSeconds);
