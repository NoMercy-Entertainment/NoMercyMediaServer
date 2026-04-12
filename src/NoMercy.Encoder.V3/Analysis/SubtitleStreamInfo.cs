namespace NoMercy.Encoder.V3.Analysis;

public record SubtitleStreamInfo(
    int Index,
    string Codec,
    string? Language,
    bool IsDefault,
    bool IsForced
)
{
    private static readonly HashSet<string> TextCodecs =
    [
        "srt",
        "subrip",
        "ass",
        "ssa",
        "webvtt",
        "mov_text",
        "text",
    ];

    public bool IsTextBased => TextCodecs.Contains(Codec.ToLowerInvariant());
}
