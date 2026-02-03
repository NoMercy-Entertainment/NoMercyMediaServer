using NoMercy.Encoder;
using NoMercy.Encoder.Dto;

namespace NoMercy.EncoderV2.Streams;

/// <summary>
/// Media file stream analysis result
/// </summary>
public class StreamAnalysis
{
    public TimeSpan Duration { get; set; }
    public long FileSize { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public List<VideoStream> VideoStreams { get; set; } = [];
    public List<AudioStream> AudioStreams { get; set; } = [];
    public List<SubtitleStream> SubtitleStreams { get; set; } = [];
    public List<Chapter> Chapters { get; set; } = [];
    public List<Attachment> Attachments { get; set; } = [];

    public bool IsHDR => VideoStreams.Any(v => v.ColorSpace == "bt2020nc" || v.ColorTransfer == "smpte2084" || v.ColorTransfer == "arib-std-b67");
    public bool HasChapters => Chapters.Count > 0;
    public bool HasSubtitles => SubtitleStreams.Count > 0;
    public bool HasAttachments => Attachments.Count > 0;

    public VideoStream? PrimaryVideoStream => VideoStreams.FirstOrDefault();
    public AudioStream? PrimaryAudioStream => AudioStreams.FirstOrDefault();
}

/// <summary>
/// Interface for stream analysis
/// </summary>
public interface IStreamAnalyzer
{
    Task<StreamAnalysis> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Analyzes media files using FFprobe
/// Provides injectable service for stream analysis with DI pattern
/// </summary>
public class StreamAnalyzer : IStreamAnalyzer
{
    public async Task<StreamAnalysis> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        Ffprobe ffprobe = new(filePath);
        await ffprobe.GetStreamData();

        FileInfo fileInfo = new(filePath);

        StreamAnalysis analysis = new()
        {
            FilePath = filePath,
            FileSize = fileInfo.Length,
            Duration = ffprobe.Format.Duration ?? TimeSpan.Zero,
            VideoStreams = ffprobe.VideoStreams,
            AudioStreams = ffprobe.AudioStreams,
            SubtitleStreams = ffprobe.SubtitleStreams,
            Chapters = ffprobe.Chapters,
            Attachments = ffprobe.Attachments
        };

        return analysis;
    }
}
