using FFMpegCore;

namespace NoMercy.Encoder;

[Serializable]
public class MediaAnalysis(IMediaAnalysis mediaAnalysis, string path)
{
    public TimeSpan Duration { get; } = mediaAnalysis.Duration;
    public MediaFormat Format { get; } = mediaAnalysis.Format;
    public AudioStream? PrimaryAudioStream { get; } = mediaAnalysis.PrimaryAudioStream;
    public VideoStream? PrimaryVideoStream { get; } = mediaAnalysis.PrimaryVideoStream;
    public SubtitleStream? PrimarySubtitleStream { get; } = mediaAnalysis.PrimarySubtitleStream;
    public List<VideoStream> VideoStreams { get; } = mediaAnalysis.VideoStreams;
    public List<AudioStream> AudioStreams { get; } = mediaAnalysis.AudioStreams;
    public List<SubtitleStream> SubtitleStreams { get; } = mediaAnalysis.SubtitleStreams;
    public IReadOnlyList<string> ErrorData { get; } = mediaAnalysis.ErrorData;
    public string Path { get; } = path;
}