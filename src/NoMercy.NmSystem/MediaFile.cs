namespace NoMercy.NmSystem;
public class MediaFile
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public int Size { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Accessed { get; set; }
    public string Type { get; set; } = string.Empty;
    public MovieFileExtend? Parsed { get; set; }

    public FFprobeData? FFprobe { get; set; }
    // public Fingerprint? FingerPint { get; init; }
}