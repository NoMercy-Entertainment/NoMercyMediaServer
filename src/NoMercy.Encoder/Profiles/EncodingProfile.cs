namespace NoMercy.Encoder.Profiles;

public record EncodingProfile(
    Ulid Id,
    string Name,
    Container Container,
    VideoOutput? Video,
    AudioOutput[] Audio,
    SubtitleOutput[] Subtitles,
    ThumbnailOutput? Thumbnails = null,
    LadderConfig? Ladder = null,
    HlsConfig? Hls = null,
    HlsDerivatives? HlsDerivatives = null,
    DashConfig? Dash = null,
    DrmConfig? Drm = null,
    int SegmentDurationSeconds = 6,
    EncodeMode EncodeMode = EncodeMode.SinglePass,
    bool AutoDetectCrop = false,
    int SchemaVersion = 2
)
{
    public string? Description { get; init; }
    public bool IsBuiltin { get; init; }

    // Default to PreferHardware so any preset (builtin or backfilled V1) hits
    // NVENC/AMF/QSV/VideoToolbox when one is present. Presets that need CPU
    // quality (Compress HEVC MKV, archival) opt into PreferQuality explicitly.
    public HardwarePreference HardwarePreference { get; init; } = HardwarePreference.PreferHardware;
    public BitDepthPolicy BitDepthPolicy { get; init; } = BitDepthPolicy.WarnAndDowngrade;
    public HdrPolicy HdrPolicy { get; init; } = HdrPolicy.PassthroughWhenPossible;
    public HdrOptions? HdrOptions { get; init; }
    public ClientCompatibility ClientCompatibility { get; init; } = ClientCompatibility.Universal;
    public SubtitleAcquisitionConfig? SubtitleAcquisition { get; init; }
    public Dictionary<string, string>? CustomArguments { get; init; }
}
