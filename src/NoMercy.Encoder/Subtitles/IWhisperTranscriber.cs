using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Subtitles;

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

    // LargeV2 retained as an available model option beyond spec
    LargeV2,
    LargeV3,
}
