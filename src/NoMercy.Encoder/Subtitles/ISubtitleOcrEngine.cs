using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Subtitles;

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
