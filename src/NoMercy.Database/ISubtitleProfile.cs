namespace NoMercy.Database;

public class ISubtitleProfile
{
    public string Codec { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public string[] AllowedLanguages { get; set; } = [];
    public string[] Opts { get; set; } = [];
    public (string key, string Val)[] CustomArguments { get; set; } = [];
}