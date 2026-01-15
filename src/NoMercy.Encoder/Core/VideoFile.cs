using NoMercy.NmSystem;

namespace NoMercy.Encoder.Core;

public class VideoFile : VideoAudioFile
{
    internal override bool IsVideo => true;

    public VideoFile(FfProbeData ffProbeData, string ffmpegPath) : base(ffProbeData, ffmpegPath)
    {
    }
}