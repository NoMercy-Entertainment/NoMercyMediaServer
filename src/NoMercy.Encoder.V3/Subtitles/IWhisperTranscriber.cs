namespace NoMercy.Encoder.V3.Subtitles;

using NoMercy.Encoder.V3.Progress;

public interface IWhisperTranscriber
{
    Task<SubtitleTrack> TranscribeAsync(
        string inputPath,
        int audioStreamIndex,
        string language,
        WhisperOptions? options,
        IProgressObserver? progress,
        CancellationToken ct
    );
}

public record WhisperOptions(
    string ModelPath,
    WhisperModelSize ModelSize = WhisperModelSize.LargeV3,
    bool TranslateToEnglish = false,
    int MaxSegmentLengthMs = 10000
);

public enum WhisperModelSize
{
    Tiny,
    Base,
    Small,
    Medium,
    LargeV2,
    LargeV3,
}
