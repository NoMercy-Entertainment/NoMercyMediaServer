namespace NoMercy.EncoderV2.Specifications.MP4;

/// <summary>
/// Represents MP4 (MPEG-4 Part 14) container specification details.
/// MP4 is based on the ISO base media file format (ISO/IEC 14496-12).
/// </summary>
public class MP4Specification
{
    /// <summary>
    /// Major brand (e.g., "isom", "mp41", "mp42", "dash")
    /// </summary>
    public string MajorBrand { get; set; } = "isom";

    /// <summary>
    /// Minor version
    /// </summary>
    public int MinorVersion { get; set; } = 512;

    /// <summary>
    /// Compatible brands list
    /// </summary>
    public List<string> CompatibleBrands { get; set; } = ["isom", "iso2", "avc1", "mp41"];

    /// <summary>
    /// Enable fast start (moov atom at beginning for streaming)
    /// </summary>
    public bool FastStart { get; set; } = true;

    /// <summary>
    /// Enable fragmented MP4 (fMP4) for adaptive streaming
    /// </summary>
    public bool Fragmented { get; set; }

    /// <summary>
    /// Fragment duration in milliseconds (for fMP4)
    /// </summary>
    public int FragmentDurationMs { get; set; } = 2000;

    /// <summary>
    /// Enable CMAF (Common Media Application Format) compatibility
    /// </summary>
    public bool CMAFCompliant { get; set; }

    /// <summary>
    /// Timescale for movie header (typically 1000 for milliseconds)
    /// </summary>
    public int MovieTimescale { get; set; } = 1000;

    /// <summary>
    /// Default language for tracks
    /// </summary>
    public string DefaultLanguage { get; set; } = "und";

    /// <summary>
    /// Enable edit list (for audio priming, delay correction)
    /// </summary>
    public bool EnableEditList { get; set; } = true;

    /// <summary>
    /// Enable negative CTS (composition time stamps)
    /// </summary>
    public bool AllowNegativeCTS { get; set; } = true;

    /// <summary>
    /// Use 64-bit chunk offsets for large files
    /// </summary>
    public bool Use64BitOffsets { get; set; }

    /// <summary>
    /// Creation time
    /// </summary>
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Modification time
    /// </summary>
    public DateTime ModificationTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents an MP4 track
/// </summary>
public class MP4Track
{
    /// <summary>
    /// Track ID (1-based)
    /// </summary>
    public int TrackId { get; set; }

    /// <summary>
    /// Track type
    /// </summary>
    public MP4TrackType TrackType { get; set; }

    /// <summary>
    /// Handler type (e.g., "vide", "soun", "text")
    /// </summary>
    public string HandlerType { get; set; } = string.Empty;

    /// <summary>
    /// Codec four-character code (e.g., "avc1", "hvc1", "mp4a")
    /// </summary>
    public string CodecFourCC { get; set; } = string.Empty;

    /// <summary>
    /// Track name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Language (ISO 639-2/T)
    /// </summary>
    public string Language { get; set; } = "und";

    /// <summary>
    /// Is this track enabled?
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Track timescale (samples per second)
    /// </summary>
    public int Timescale { get; set; }

    /// <summary>
    /// Track duration in timescale units
    /// </summary>
    public long Duration { get; set; }

    /// <summary>
    /// Track volume (for audio, 1.0 = full)
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Video width (for video tracks)
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Video height (for video tracks)
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Audio sample rate (for audio tracks)
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Audio channel count (for audio tracks)
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Decoder configuration (codec-specific)
    /// </summary>
    public byte[]? DecoderConfig { get; set; }
}

/// <summary>
/// MP4 track types
/// </summary>
public enum MP4TrackType
{
    Video,
    Audio,
    Text,
    Subtitle,
    Hint,
    Meta,
    Auxv
}

/// <summary>
/// MP4 box/atom types
/// </summary>
public static class MP4BoxTypes
{
    // File level
    public const string FileType = "ftyp";
    public const string Movie = "moov";
    public const string MediaData = "mdat";
    public const string Free = "free";
    public const string Skip = "skip";

    // Movie level
    public const string MovieHeader = "mvhd";
    public const string Track = "trak";
    public const string MovieExtends = "mvex";
    public const string UserData = "udta";

    // Track level
    public const string TrackHeader = "tkhd";
    public const string Media = "mdia";
    public const string TrackReference = "tref";
    public const string EditList = "edts";

    // Media level
    public const string MediaHeader = "mdhd";
    public const string Handler = "hdlr";
    public const string MediaInformation = "minf";

    // Sample table
    public const string SampleTable = "stbl";
    public const string SampleDescription = "stsd";
    public const string TimeToSample = "stts";
    public const string CompositionOffset = "ctts";
    public const string SyncSample = "stss";
    public const string SampleToChunk = "stsc";
    public const string SampleSize = "stsz";
    public const string ChunkOffset = "stco";
    public const string ChunkOffset64 = "co64";

    // Fragmented MP4
    public const string MovieFragment = "moof";
    public const string MovieFragmentHeader = "mfhd";
    public const string TrackFragment = "traf";
    public const string TrackFragmentHeader = "tfhd";
    public const string TrackFragmentRun = "trun";
    public const string TrackFragmentDecodeTime = "tfdt";
    public const string SegmentIndex = "sidx";
}

/// <summary>
/// Common MP4 codec four-character codes
/// </summary>
public static class MP4CodecFourCC
{
    // Video codecs
    public const string AVC1 = "avc1";  // H.264/AVC baseline/main/high
    public const string AVC3 = "avc3";  // H.264/AVC with in-band SPS/PPS
    public const string HVC1 = "hvc1";  // H.265/HEVC with out-of-band VPS/SPS/PPS
    public const string HEV1 = "hev1";  // H.265/HEVC with in-band VPS/SPS/PPS
    public const string AV01 = "av01";  // AV1
    public const string VP09 = "vp09";  // VP9
    public const string MP4V = "mp4v";  // MPEG-4 Part 2

    // Audio codecs
    public const string MP4A = "mp4a";  // AAC, MP3, etc. (MPEG-4 Audio)
    public const string AC3 = "ac-3";   // AC-3 (Dolby Digital)
    public const string EAC3 = "ec-3";  // E-AC-3 (Dolby Digital Plus)
    public const string OPUS = "Opus";  // Opus
    public const string FLAC = "fLaC";  // FLAC
    public const string ALAC = "alac";  // Apple Lossless
    public const string DTSC = "dtsc";  // DTS Core
    public const string DTSH = "dtsh";  // DTS-HD High Resolution
    public const string DTSL = "dtsl";  // DTS-HD Master Audio
    public const string DTSE = "dtse";  // DTS Express

    // Subtitle/Text codecs
    public const string TX3G = "tx3g";  // 3GPP Timed Text
    public const string WVTT = "wvtt";  // WebVTT
    public const string STPP = "stpp";  // TTML in ISOBMFF
    public const string C608 = "c608";  // CEA-608 Closed Captions
    public const string C708 = "c708";  // CEA-708 Closed Captions
}

/// <summary>
/// Common MP4 brand identifiers
/// </summary>
public static class MP4Brands
{
    // Base brands
    public const string ISOM = "isom";  // ISO Base Media File Format
    public const string ISO2 = "iso2";  // ISO/IEC 14496-12:2005
    public const string ISO3 = "iso3";  // ISO/IEC 14496-12:2008
    public const string ISO4 = "iso4";  // ISO/IEC 14496-12:2009
    public const string ISO5 = "iso5";  // ISO/IEC 14496-12:2012
    public const string ISO6 = "iso6";  // ISO/IEC 14496-12:2015

    // MP4 brands
    public const string MP41 = "mp41";  // MP4 v1
    public const string MP42 = "mp42";  // MP4 v2

    // Codec-specific brands
    public const string AVC1 = "avc1";  // H.264/AVC
    public const string HVC1 = "hvc1";  // H.265/HEVC
    public const string AV01 = "av01";  // AV1

    // Streaming brands
    public const string DASH = "dash";  // DASH
    public const string MSDH = "msdh";  // DASH Segment
    public const string MSIX = "msix";  // DASH Indexed Segment
    public const string CMFC = "cmfc";  // CMAF
    public const string CMFF = "cmff";  // CMAF Fragment

    // Apple brands
    public const string M4V = "M4V ";   // Apple M4V (iTunes video)
    public const string M4A = "M4A ";   // Apple M4A (iTunes audio)
    public const string M4P = "M4P ";   // Apple M4P (protected)
    public const string QT = "qt  ";    // QuickTime
}
