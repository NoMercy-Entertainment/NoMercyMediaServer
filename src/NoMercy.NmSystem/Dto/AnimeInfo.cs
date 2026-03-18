namespace NoMercy.NmSystem.Dto;

public class AnimeInfo
{
    public string FileName { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string? Title { get; set; }
    public string? ExtraInfo { get; set; }
    public string? Checksum { get; set; }
    public string? Extension { get; set; }
}