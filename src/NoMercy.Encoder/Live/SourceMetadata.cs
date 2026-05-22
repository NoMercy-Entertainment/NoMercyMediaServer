namespace NoMercy.Encoder.Live;

/// <summary>
///     What the source file looks like — enough for the quality selector to decide whether each
///     client capability can be satisfied as-is or needs transcoding.
/// </summary>
public sealed record SourceMetadata
{
    public string VideoCodec { get; init; } = string.Empty;
    public string AudioCodec { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public int BitrateKbps { get; init; }
    public bool IsHdr { get; init; }
}
