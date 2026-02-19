namespace NoMercy.Plugins.Abstractions;

public class MediaMetadata
{
    public required string Title { get; init; }
    public string? Overview { get; init; }
    public int? Year { get; init; }
    public string? PosterUrl { get; init; }
    public string? BackdropUrl { get; init; }
    public List<string> Genres { get; init; } = [];
    public double? Rating { get; init; }
    public string? ExternalId { get; init; }
    public string? ExternalSource { get; init; }
}
