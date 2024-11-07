using FFMpegCore;

namespace NoMercy.NmSystem;
public class FFprobeData
{
    public TimeSpan Duration { get; set; }
    public MediaFormat Format { get; set; } = new();
    public AudioStream? PrimaryAudioStream { get; set; }
    public VideoStream? PrimaryVideoStream { get; set; }
    public SubtitleStream? PrimarySubtitleStream { get; set; }
    public List<VideoStream> VideoStreams { get; set; } = new();
    public List<AudioStream> AudioStreams { get; set; } = new();
    public List<SubtitleStream> SubtitleStreams { get; set; } = new();
    public IReadOnlyList<string> ErrorData { get; set; } = new List<string>();
}