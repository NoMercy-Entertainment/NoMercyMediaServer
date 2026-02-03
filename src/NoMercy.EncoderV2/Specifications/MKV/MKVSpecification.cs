namespace NoMercy.EncoderV2.Specifications.MKV;

/// <summary>
/// Represents MKV (Matroska) container specification details.
/// MKV is a flexible, open-source container format supporting virtually any codec.
/// </summary>
public class MKVSpecification
{
    /// <summary>
    /// Matroska DocType version (typically 4 for modern MKV files)
    /// </summary>
    public int DocTypeVersion { get; set; } = 4;

    /// <summary>
    /// Minimum DocType version required to read this file
    /// </summary>
    public int DocTypeReadVersion { get; set; } = 2;

    /// <summary>
    /// Enable chapters support
    /// </summary>
    public bool EnableChapters { get; set; } = true;

    /// <summary>
    /// Enable attachments (fonts, cover art, etc.)
    /// </summary>
    public bool EnableAttachments { get; set; } = true;

    /// <summary>
    /// Enable tags/metadata
    /// </summary>
    public bool EnableTags { get; set; } = true;

    /// <summary>
    /// Enable cue points for seeking
    /// </summary>
    public bool EnableCues { get; set; } = true;

    /// <summary>
    /// Default track language
    /// </summary>
    public string DefaultLanguage { get; set; } = "eng";

    /// <summary>
    /// Segment title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Muxing application name
    /// </summary>
    public string MuxingApp { get; set; } = "NoMercy EncoderV2";

    /// <summary>
    /// Writing application name
    /// </summary>
    public string WritingApp { get; set; } = "NoMercy EncoderV2";

    /// <summary>
    /// Cluster size in bytes (default 32KB)
    /// </summary>
    public int ClusterSizeBytes { get; set; } = 32768;

    /// <summary>
    /// Timestamp scale in nanoseconds (default 1000000 = 1ms precision)
    /// </summary>
    public long TimestampScale { get; set; } = 1000000;
}

/// <summary>
/// Represents an MKV track
/// </summary>
public class MKVTrack
{
    /// <summary>
    /// Track number (1-based)
    /// </summary>
    public int TrackNumber { get; set; }

    /// <summary>
    /// Track UID (unique identifier)
    /// </summary>
    public ulong TrackUid { get; set; }

    /// <summary>
    /// Track type: video, audio, subtitle, button, control, metadata
    /// </summary>
    public MKVTrackType TrackType { get; set; }

    /// <summary>
    /// Codec ID (e.g., V_MPEG4/ISO/AVC for H.264)
    /// </summary>
    public string CodecId { get; set; } = string.Empty;

    /// <summary>
    /// Track name/title
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Language (BCP-47 or ISO 639-2)
    /// </summary>
    public string Language { get; set; } = "und";

    /// <summary>
    /// Is this the default track for its type?
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Is this track forced (e.g., forced subtitles)?
    /// </summary>
    public bool IsForced { get; set; }

    /// <summary>
    /// Is this track enabled?
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Track-specific codec private data
    /// </summary>
    public byte[]? CodecPrivate { get; set; }
}

/// <summary>
/// MKV track types
/// </summary>
public enum MKVTrackType
{
    Video = 1,
    Audio = 2,
    Complex = 3,
    Logo = 16,
    Subtitle = 17,
    Buttons = 18,
    Control = 32,
    Metadata = 33
}

/// <summary>
/// Represents an MKV chapter
/// </summary>
public class MKVChapter
{
    /// <summary>
    /// Chapter UID
    /// </summary>
    public ulong ChapterUid { get; set; }

    /// <summary>
    /// Chapter start time
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// Chapter end time (optional)
    /// </summary>
    public TimeSpan? EndTime { get; set; }

    /// <summary>
    /// Chapter display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Chapter language
    /// </summary>
    public string Language { get; set; } = "eng";

    /// <summary>
    /// Is this chapter hidden?
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Is this chapter enabled?
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Represents an MKV attachment
/// </summary>
public class MKVAttachment
{
    /// <summary>
    /// Attachment UID
    /// </summary>
    public ulong AttachmentUid { get; set; }

    /// <summary>
    /// File name
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME type
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// File description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// File data
    /// </summary>
    public byte[] Data { get; set; } = [];
}

/// <summary>
/// Common MKV codec IDs
/// </summary>
public static class MKVCodecIds
{
    // Video codecs
    public const string H264 = "V_MPEG4/ISO/AVC";
    public const string H265 = "V_MPEGH/ISO/HEVC";
    public const string VP8 = "V_VP8";
    public const string VP9 = "V_VP9";
    public const string AV1 = "V_AV1";
    public const string MPEG4 = "V_MPEG4/ISO/ASP";
    public const string MPEG2 = "V_MPEG2";
    public const string Theora = "V_THEORA";

    // Audio codecs
    public const string AAC = "A_AAC";
    public const string AC3 = "A_AC3";
    public const string EAC3 = "A_EAC3";
    public const string DTS = "A_DTS";
    public const string DTSHD = "A_DTS/LOSSLESS";
    public const string TrueHD = "A_TRUEHD";
    public const string FLAC = "A_FLAC";
    public const string MP3 = "A_MPEG/L3";
    public const string Opus = "A_OPUS";
    public const string Vorbis = "A_VORBIS";
    public const string PCM = "A_PCM/INT/LIT";

    // Subtitle codecs
    public const string SRT = "S_TEXT/UTF8";
    public const string ASS = "S_TEXT/ASS";
    public const string SSA = "S_TEXT/SSA";
    public const string VobSub = "S_VOBSUB";
    public const string PGS = "S_HDMV/PGS";
    public const string DVBSub = "S_DVBSUB";
    public const string WebVTT = "S_TEXT/WEBVTT";
}
