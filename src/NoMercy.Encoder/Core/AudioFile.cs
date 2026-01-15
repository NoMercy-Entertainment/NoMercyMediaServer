using NoMercy.NmSystem;

namespace NoMercy.Encoder.Core;

public class AudioFile : VideoAudioFile
{
    internal override bool IsAudio => true;

    public AudioFile(FfProbeData ffProbeData, string ffmpegPath) : base(ffProbeData, ffmpegPath)
    {
    }
}