namespace NoMercy.NmSystem;

public static class FfProbe
{
    public static FfProbeData Create(string file)
    {
        try
        {
            FFMpegCore.IMediaAnalysis analysis = FFMpegCore.FFProbe.Analyse(file);
            return new()
            {
                FilePath = file,
                Duration = analysis.Duration,
                Format = analysis.Format,
                PrimaryAudioStream = analysis.PrimaryAudioStream,
                PrimaryVideoStream = analysis.PrimaryVideoStream,
                PrimarySubtitleStream = analysis.PrimarySubtitleStream,
                VideoStreams = analysis.VideoStreams,
                AudioStreams = analysis.AudioStreams,
                SubtitleStreams = analysis.SubtitleStreams,
                ErrorData = analysis.ErrorData
            };
        }
        catch (Exception ex)
        {
            return new()
            {
                ErrorData = new List<string> { ex.Message }
            };
        }
    }
}