namespace NoMercy.Encoder.Core;

public class VideoFile : VideoAudioFile
{
    internal override bool IsVideo => true;

    public VideoFile(MediaAnalysis fMediaAnalysis, string ffmpegPath) : base(fMediaAnalysis, ffmpegPath)
    {
    }
}