namespace NoMercy.EncoderV2.Specifications.HLS;

/// <summary>
/// Represents HLS container/playlist specification details
/// </summary>
public class HLSSpecification
{
    public int Version { get; set; } = 3;
    public int TargetDuration { get; set; } = 10;
    public int SegmentDuration { get; set; } = 6;
    public string PlaylistType { get; set; } = "VOD";
    public bool AllowCache { get; set; } = true;
    public int MediaSequence { get; set; } = 0;
    public bool IndependentSegments { get; set; } = true;
    public string SegmentFilenameFormat { get; set; } = "segment_%05d.ts";
    public string PlaylistFilename { get; set; } = "playlist.m3u8";
    public bool IncludeIFramePlaylist { get; set; } = false;
}

/// <summary>
/// Represents an HLS variant stream
/// </summary>
public class HLSVariantStream
{
    public int Bandwidth { get; set; }
    public int AverageBandwidth { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public double Framerate { get; set; }
    public string Codecs { get; set; } = string.Empty;
    public string PlaylistUri { get; set; } = string.Empty;
    public string? AudioGroup { get; set; }
    public string? SubtitleGroup { get; set; }
}

/// <summary>
/// Represents an HLS media group (audio, subtitles, etc.)
/// </summary>
public class HLSMediaGroup
{
    public string Type { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public bool IsDefault { get; set; }
    public bool Autoselect { get; set; }
    public string? Uri { get; set; }
}

