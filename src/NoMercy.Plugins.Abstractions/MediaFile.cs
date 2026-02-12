namespace NoMercy.Plugins.Abstractions;

public class MediaFile
{
    public required string Path { get; init; }
    public required string FileName { get; init; }
    public long Size { get; init; }
    public MediaType Type { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}
