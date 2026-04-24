namespace NoMercy.Encoder.Profiles;

using NoMercy.Encoder.Codecs;

public record EncodingProfile(
    Ulid Id,
    string Name,
    OutputFormat Format,
    VideoOutput[] VideoOutputs,
    AudioOutput[] AudioOutputs,
    SubtitleOutput[] SubtitleOutputs,
    ThumbnailOutput? Thumbnails = null,
    int SegmentDurationSeconds = 6,
    EncodeMode EncodeMode = EncodeMode.SinglePass,
    bool AutoLadder = false,
    bool AutoDetectCrop = false,
    BuildingBlocks.Drm.DrmConfig? Drm = null,
    int SchemaVersion = 1
)
{
    /// <summary>Human-readable description of what this profile is intended for.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional parent profile whose settings this profile inherits from.
    /// Cycle detection is enforced by the validator.
    /// </summary>
    public Ulid? ParentId { get; init; }

    /// <summary>
    /// When true this profile ships with the encoder and cannot be deleted by users.
    /// </summary>
    public bool IsBuiltin { get; init; }

    /// <summary>Hardware acceleration preference for this profile.</summary>
    public HardwarePreference HardwarePreference { get; init; } = HardwarePreference.PreferHardware;

    /// <summary>
    /// Controls what happens when the encoder cannot honour the requested bit depth.
    /// </summary>
    public BitDepthPolicy BitDepthPolicy { get; init; } = BitDepthPolicy.WarnAndDowngrade;

    /// <summary>How HDR metadata should be handled for streams encoded with this profile.</summary>
    public HdrPolicy HdrPolicy { get; init; } = HdrPolicy.PassthroughWhenPossible;

    /// <summary>
    /// Short-hand tonemap algorithm name (hable | mobius | reinhard | clip | bt2390).
    /// Overridden by <see cref="HdrOptions"/> when both are set.
    /// </summary>
    public string? TonemapAlgorithm { get; init; }

    /// <summary>Full HDR tonemapping options. Takes precedence over TonemapAlgorithm.</summary>
    public HdrOptions? HdrOptions { get; init; }

    /// <summary>
    /// Explicit ABR ladder rungs used when AutoLadder is false and the caller wants
    /// a fixed set of resolutions instead of the profile-defined VideoOutputs.
    /// </summary>
    public LadderRung[]? LadderRungs { get; init; }

    /// <summary>
    /// Fingerprint of the publisher key used to sign this profile, for integrity verification.
    /// </summary>
    public string? PublisherKeyFingerprint { get; init; }

    /// <summary>Detached signature over the canonical profile JSON.</summary>
    public string? Signature { get; init; }

    /// <summary>Arbitrary extra FFmpeg arguments appended after all generated arguments.</summary>
    public Dictionary<string, string>? CustomArguments { get; init; }

    /// <summary>
    /// HLS muxer options (playlist type, segment container, independent-segments flag).
    /// Only meaningful when <see cref="Format"/> is <see cref="OutputFormat.Hls"/>.
    /// Null means use defaults (vod / mpegts / IndependentSegments=false).
    /// </summary>
    public HlsOptions? HlsOptions { get; init; }
}

public record VideoOutput(
    VideoCodecType Codec,
    int Width,
    int? Height,
    int BitrateKbps,
    int Crf,
    string? Preset,
    string? Profile,
    string? Level,
    bool ConvertHdrToSdr,
    int KeyframeIntervalSeconds,
    bool TenBit,
    string SegmentNameTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
    string PlaylistNameTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
    string? Tune = null,
    string? ColorSpace = null,
    Dictionary<string, string>? CustomArguments = null
)
{
    /// <summary>Explicit bit depth (8 or 10). Null means derive from TenBit and the source.</summary>
    public int? BitDepth { get; init; }

    /// <summary>
    /// FFmpeg pixel format string (e.g. yuv420p, yuv420p10le).
    /// When null the encoder selects a format compatible with the codec and TenBit flag.
    /// </summary>
    public string? PixelFormat { get; init; }

    /// <summary>VBR/CRF-capped ceiling bitrate in kbps. Null means uncapped.</summary>
    public int? MaxBitrateKbps { get; init; }

    /// <summary>Encoder buffer size in kbps for VBV/HRD compliance. Null means encoder default.</summary>
    public int? BufferSizeKbps { get; init; }

    /// <summary>Rate control mode selector used by the plan stage to choose a RateControl variant.</summary>
    public RateControlMode RateControlMode { get; init; } = RateControlMode.Crf;

    /// <summary>H.264/H.265 codec profile selector. Auto lets the encoder decide.</summary>
    public CodecProfile CodecProfile { get; init; } = CodecProfile.Auto;
}

public record AudioOutput(
    AudioCodecType Codec,
    int BitrateKbps,
    int Channels,
    int SampleRateHz,
    string[] AllowedLanguages,
    Audio.LoudnessMode Loudness = Audio.LoudnessMode.None,
    Audio.DownmixMode Downmix = Audio.DownmixMode.Auto,
    string? CustomPanMatrix = null,
    string SegmentNameTemplate = ":type:_:language:_:codec:/:type:_:language:_:codec:",
    string PlaylistNameTemplate = ":type:_:language:_:codec:/:type:_:language:_:codec:",
    Dictionary<string, string>? CustomArguments = null
)
{
    /// <summary>
    /// BCP-47 language tag to use when the source stream carries no language metadata.
    /// </summary>
    public string? DefaultLanguage { get; init; }
}

public record SubtitleOutput(
    SubtitleCodecType Codec,
    SubtitleMode Mode,
    string[] AllowedLanguages,
    string PlaylistNameTemplate = "subtitles/:filename:.:language:.:variant:",
    Dictionary<string, string>? CustomArguments = null
)
{
    /// <summary>When true, forced subtitle tracks are included even when AllowedLanguages is empty.</summary>
    public bool IncludeForced { get; init; }

    /// <summary>
    /// Tesseract language code used when OCR is required for bitmap subtitle tracks
    /// (e.g. PGS/VOBSUB). Null means the encoder will attempt auto-detection.
    /// </summary>
    public string? OcrLanguage { get; init; }
}

public record ThumbnailOutput(int Width, int IntervalSeconds);
