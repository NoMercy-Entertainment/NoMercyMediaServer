namespace NoMercy.Encoder.Core;

public class AudioFile : VideoAudioFile
{
    internal override bool IsAudio => true;

    public AudioFile(MediaAnalysis fMediaAnalysis, string ffmpegPath) : base(fMediaAnalysis, ffmpegPath)
    {
    }
}