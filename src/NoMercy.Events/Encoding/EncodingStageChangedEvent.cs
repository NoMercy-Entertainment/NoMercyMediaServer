namespace NoMercy.Events.Encoding;

public sealed class EncodingStageChangedEvent : EventBase
{
    public override string Source => "Encoder";

    public required dynamic JobId { get; init; }
    public required string Status { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string BaseFolder { get; init; } = string.Empty;
    public string ShareBasePath { get; init; } = string.Empty;
    public List<string> VideoStreams { get; init; } = [];
    public List<string> AudioStreams { get; init; } = [];
    public List<string> SubtitleStreams { get; init; } = [];
    public bool HasGpu { get; init; }
    public bool IsHdr { get; init; }
}
