namespace NoMercy.Database;

public class IVideoProfile
{
    public string Codec { get; set; } = string.Empty;
    public int Bitrate { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Framerate { get; set; }
    public string Preset { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Tune { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public string ColorSpace { get; set; } = string.Empty;
    public int Crf { get; set; }
    public int KeyInt { get; set; }
    public string[] Opts { get; set; } = [];
    public CustomArgument[] CustomArguments { get; set; } = [];
    public bool ConvertHdrToSdr { get; set; }
}