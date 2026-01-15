namespace NoMercy.NmSystem.Dto;

public class MovieFileExtend
{
    public string? Title { get; init; }
    public string? Year { get; init; }
    public bool IsSeries { get; set; }
    public int? Season { get; init; }
    public int? Episode { get; init; }
    public bool IsSuccess { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public int DiscNumber { get; set; }
    public int TrackNumber { get; set; }
}