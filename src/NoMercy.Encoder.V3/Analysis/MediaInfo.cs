namespace NoMercy.Encoder.V3.Analysis;

public record DolbyVisionInfo(
    int Profile,
    int Level,
    bool HasRpu,
    bool HasEl,
    DvBlCompatibility BlCompat
);

public enum DvBlCompatibility
{
    None,
    Hdr10,
    Sdr,
}

public record CropArea(int W, int H, int X, int Y);

public record MediaInfo(
    string FilePath,
    string Format,
    TimeSpan Duration,
    long OverallBitRateKbps,
    long FileSizeBytes,
    IReadOnlyList<VideoStreamInfo> VideoStreams,
    IReadOnlyList<AudioStreamInfo> AudioStreams,
    IReadOnlyList<SubtitleStreamInfo> SubtitleStreams,
    IReadOnlyList<ChapterInfo> Chapters,
    DolbyVisionInfo? DolbyVision = null,
    bool HasHdr10Plus = false,
    CropArea? DetectedCrop = null,
    double CropAspectRatio = 0
)
{
    public bool HasVideo => VideoStreams.Count > 0;
    public bool HasAudio => AudioStreams.Count > 0;
    public bool HasSubtitles => SubtitleStreams.Count > 0;

    public bool IsHdr => VideoStreams.Any(v => v.IsHdr);
    public int PrimaryBitDepth => VideoStreams.FirstOrDefault()?.BitDepth ?? 8;
    public bool IsVariableFrameRate => VideoStreams.Any(v => v.IsVariableFrameRate);
    public double PrimaryFrameRate => VideoStreams.FirstOrDefault()?.AverageFrameRate ?? 0;
    public int PrimaryRotation => VideoStreams.FirstOrDefault()?.Rotation ?? 0;
}
