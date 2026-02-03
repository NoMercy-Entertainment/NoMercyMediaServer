namespace NoMercy.EncoderV2.Abstractions;

/// <summary>
/// Represents a complete encoding profile with all settings
/// </summary>
public interface IEncodingProfile
{
    /// <summary>
    /// Unique identifier for the profile
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of the profile's purpose
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Whether this is a system-provided profile
    /// </summary>
    bool IsSystem { get; }

    /// <summary>
    /// The container format to use
    /// </summary>
    IContainer Container { get; }

    /// <summary>
    /// Video encoding settings (null for audio-only)
    /// </summary>
    IReadOnlyList<VideoOutputConfig> VideoOutputs { get; }

    /// <summary>
    /// Audio encoding settings (null for video-only)
    /// </summary>
    IReadOnlyList<AudioOutputConfig> AudioOutputs { get; }

    /// <summary>
    /// Subtitle handling settings
    /// </summary>
    IReadOnlyList<SubtitleOutputConfig> SubtitleOutputs { get; }

    /// <summary>
    /// Thumbnail/sprite generation settings
    /// </summary>
    ThumbnailConfig? ThumbnailConfig { get; }

    /// <summary>
    /// Global encoding options
    /// </summary>
    EncodingOptions Options { get; }

    /// <summary>
    /// Validates the entire profile configuration
    /// </summary>
    ValidationResult Validate();
}

/// <summary>
/// Configuration for a video output stream
/// </summary>
public sealed class VideoOutputConfig
{
    /// <summary>
    /// Output identifier (e.g., "1080p", "720p")
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The video codec to use
    /// </summary>
    public required IVideoCodec Codec { get; init; }

    /// <summary>
    /// Target width (null to preserve aspect ratio)
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Target height (null to preserve aspect ratio)
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Scale filter mode
    /// </summary>
    public ScaleMode ScaleMode { get; init; } = ScaleMode.Fit;

    /// <summary>
    /// Whether to enable HDR to SDR tone mapping
    /// </summary>
    public bool ToneMap { get; init; }

    /// <summary>
    /// Additional filters to apply
    /// </summary>
    public IReadOnlyList<string> Filters { get; init; } = [];

    /// <summary>
    /// Whether to skip encoding if source resolution is lower
    /// </summary>
    public bool SkipIfLowerResolution { get; init; } = true;
}

/// <summary>
/// Configuration for an audio output stream
/// </summary>
public sealed class AudioOutputConfig
{
    /// <summary>
    /// Output identifier (e.g., "stereo", "surround")
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The audio codec to use
    /// </summary>
    public required IAudioCodec Codec { get; init; }

    /// <summary>
    /// Language filter (null for all languages)
    /// </summary>
    public IReadOnlyList<string>? Languages { get; init; }

    /// <summary>
    /// Whether to include only the default audio track
    /// </summary>
    public bool DefaultTrackOnly { get; init; }

    /// <summary>
    /// Audio filters to apply
    /// </summary>
    public IReadOnlyList<string> Filters { get; init; } = [];
}

/// <summary>
/// Configuration for a subtitle output stream
/// </summary>
public sealed class SubtitleOutputConfig
{
    /// <summary>
    /// Output identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The subtitle codec to use
    /// </summary>
    public required ISubtitleCodec Codec { get; init; }

    /// <summary>
    /// Language filter (null for all languages)
    /// </summary>
    public IReadOnlyList<string>? Languages { get; init; }

    /// <summary>
    /// Whether to include only the default subtitle track
    /// </summary>
    public bool DefaultTrackOnly { get; init; }

    /// <summary>
    /// Whether to include forced subtitles
    /// </summary>
    public bool IncludeForced { get; init; } = true;
}

/// <summary>
/// Configuration for thumbnail/sprite generation
/// </summary>
public sealed class ThumbnailConfig
{
    /// <summary>
    /// Interval between thumbnails in seconds
    /// </summary>
    public double IntervalSeconds { get; init; } = 10;

    /// <summary>
    /// Thumbnail width
    /// </summary>
    public int Width { get; init; } = 320;

    /// <summary>
    /// Thumbnail height (null to preserve aspect ratio)
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Output format (jpeg, png, webp)
    /// </summary>
    public string Format { get; init; } = "jpeg";

    /// <summary>
    /// Quality (1-100)
    /// </summary>
    public int Quality { get; init; } = 75;

    /// <summary>
    /// Whether to generate a sprite sheet
    /// </summary>
    public bool GenerateSprite { get; init; } = true;

    /// <summary>
    /// Number of columns in sprite sheet
    /// </summary>
    public int SpriteColumns { get; init; } = 10;
}

/// <summary>
/// Global encoding options
/// </summary>
public sealed class EncodingOptions
{
    /// <summary>
    /// Number of threads to use (0 for auto)
    /// </summary>
    public int Threads { get; init; }

    /// <summary>
    /// Whether to use hardware acceleration when available
    /// </summary>
    public bool UseHardwareAcceleration { get; init; } = true;

    /// <summary>
    /// Preferred hardware acceleration type
    /// </summary>
    public HardwareAcceleration? PreferredHardwareAcceleration { get; init; }

    /// <summary>
    /// Whether to overwrite existing output files
    /// </summary>
    public bool OverwriteOutput { get; init; } = true;

    /// <summary>
    /// Whether to enable two-pass encoding for video
    /// </summary>
    public bool TwoPassEncoding { get; init; }

    /// <summary>
    /// Additional global FFmpeg arguments
    /// </summary>
    public IReadOnlyList<string> AdditionalArguments { get; init; } = [];

    /// <summary>
    /// Maximum concurrent encoding jobs
    /// </summary>
    public int MaxConcurrentJobs { get; init; } = 1;

    /// <summary>
    /// Output base path
    /// </summary>
    public string? OutputBasePath { get; init; }
}

public enum ScaleMode
{
    /// <summary>
    /// Scale to fit within dimensions, preserving aspect ratio
    /// </summary>
    Fit,

    /// <summary>
    /// Scale to fill dimensions, cropping if necessary
    /// </summary>
    Fill,

    /// <summary>
    /// Scale to exact dimensions, potentially distorting
    /// </summary>
    Stretch,

    /// <summary>
    /// Scale down only, never upscale
    /// </summary>
    DownscaleOnly
}
