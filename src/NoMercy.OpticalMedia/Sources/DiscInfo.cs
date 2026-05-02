using NoMercy.Encoder.Analysis;
using NoMercy.NmSystem.Dto;

namespace NoMercy.OpticalMedia.Sources;

public record DiscInfo(
    OpticalDiscType Type,
    string? DiscLabel,
    DiscTitle[] Titles,
    DiscTrack[]? AudioTracks,
    TimeSpan TotalDuration
);

public record DiscTitle(
    int Index,
    string? Name,
    TimeSpan Duration,
    VideoStreamInfo[] VideoStreams,
    AudioStreamInfo[] AudioStreams,
    SubtitleStreamInfo[] Subtitles,
    ChapterInfo[] Chapters,
    long EstimatedSizeBytes,
    bool IsMainFeature
);

public record DiscTrack(
    int Index,
    string? Title,
    string? Artist,
    TimeSpan Duration,
    int SampleRate,
    int Channels
);
