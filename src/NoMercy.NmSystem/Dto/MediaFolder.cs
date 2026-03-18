namespace NoMercy.NmSystem.Dto;

public class MediaFolder
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Accessed { get; set; }
    public string Type { get; set; } = string.Empty;
    public MovieFileExtend Parsed { get; init; } = new();
}