namespace NoMercy.Encoder.V3.LiveTranscode;

public enum PlaybackAction
{
    DirectPlay,
    Remux,
    TranscodeAudio,
    TranscodeVideo,
}

public record PlaybackDecision(
    PlaybackAction Action,
    string? Reason,
    string? DirectStreamUrl,
    LiveQuality? RecommendedQuality = null
);
