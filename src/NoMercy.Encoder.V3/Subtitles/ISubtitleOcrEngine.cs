namespace NoMercy.Encoder.V3.Subtitles;

using NoMercy.Encoder.V3.Codecs;

public interface ISubtitleOcrEngine
{
    Task<SubtitleTrack> OcrAsync(
        string inputPath,
        int streamIndex,
        string language,
        SubtitleCodecType outputFormat,
        CancellationToken ct
    );
}

public record SubtitleTrack(
    string FilePath,
    string Language,
    SubtitleCodecType Format,
    int CueCount
);
