namespace NoMercy.Encoder.Profiles.V2;

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
    public HardwarePreference HardwarePreference { get; init; } = HardwarePreference.PreferQuality;
    public BitDepthPolicy BitDepthPolicy { get; init; } = BitDepthPolicy.WarnAndDowngrade;
    public HdrPolicy HdrPolicy { get; init; } = HdrPolicy.PassthroughWhenPossible;
    public HdrOptions? HdrOptions { get; init; }
    public ClientCompatibility ClientCompatibility { get; init; } = ClientCompatibility.Universal;
    public SubtitleAcquisitionConfig? SubtitleAcquisition { get; init; }
    public Dictionary<string, string>? CustomArguments { get; init; }
}
