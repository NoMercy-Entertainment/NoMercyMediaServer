namespace NoMercy.EncoderV2.Abstractions;

/// <summary>
/// Base interface for all codecs
/// </summary>
public interface ICodec
{
    /// <summary>
    /// The FFmpeg codec name (e.g., "libx264", "aac")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable display name
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// The codec type (Video, Audio, Subtitle)
    /// </summary>
    CodecType Type { get; }

    /// <summary>
    /// Whether this codec requires hardware acceleration
    /// </summary>
    bool RequiresHardwareAcceleration { get; }

    /// <summary>
    /// The hardware acceleration type required (if any)
    /// </summary>
    HardwareAcceleration? HardwareAccelerationType { get; }

    /// <summary>
    /// Builds the FFmpeg arguments for this codec
    /// </summary>
    IReadOnlyList<string> BuildArguments();

    /// <summary>
    /// Validates the current configuration
    /// </summary>
    ValidationResult Validate();
}

/// <summary>
/// Video codec interface with video-specific settings
/// </summary>
public interface IVideoCodec : ICodec
{
    /// <summary>
    /// Available presets for this codec
    /// </summary>
    IReadOnlyList<string> AvailablePresets { get; }

    /// <summary>
    /// Available profiles for this codec
    /// </summary>
    IReadOnlyList<string> AvailableProfiles { get; }

    /// <summary>
    /// Available tune options for this codec
    /// </summary>
    IReadOnlyList<string> AvailableTunes { get; }

    /// <summary>
    /// CRF range (min, max)
    /// </summary>
    (int Min, int Max) CrfRange { get; }

    /// <summary>
    /// Whether the codec supports B-frames
    /// </summary>
    bool SupportsBFrames { get; }

    /// <summary>
    /// The preset to use
    /// </summary>
    string? Preset { get; set; }

    /// <summary>
    /// The profile to use
    /// </summary>
    string? Profile { get; set; }

    /// <summary>
    /// The tune setting to use
    /// </summary>
    string? Tune { get; set; }

    /// <summary>
    /// Constant Rate Factor (quality-based encoding)
    /// </summary>
    int? Crf { get; set; }

    /// <summary>
    /// Target bitrate in kbps (bitrate-based encoding)
    /// </summary>
    int? Bitrate { get; set; }

    /// <summary>
    /// Maximum bitrate in kbps
    /// </summary>
    int? MaxBitrate { get; set; }

    /// <summary>
    /// Buffer size in kbps
    /// </summary>
    int? BufferSize { get; set; }

    /// <summary>
    /// Pixel format (e.g., "yuv420p")
    /// </summary>
    string? PixelFormat { get; set; }

    /// <summary>
    /// Number of B-frames
    /// </summary>
    int? BFrames { get; set; }

    /// <summary>
    /// Keyframe interval in frames
    /// </summary>
    int? KeyframeInterval { get; set; }

    /// <summary>
    /// Creates a configured clone of this codec
    /// </summary>
    IVideoCodec Clone();
}

/// <summary>
/// Audio codec interface with audio-specific settings
/// </summary>
public interface IAudioCodec : ICodec
{
    /// <summary>
    /// Available channel layouts
    /// </summary>
    IReadOnlyList<string> AvailableChannelLayouts { get; }

    /// <summary>
    /// Available sample rates
    /// </summary>
    IReadOnlyList<int> AvailableSampleRates { get; }

    /// <summary>
    /// Bitrate in kbps
    /// </summary>
    int? Bitrate { get; set; }

    /// <summary>
    /// Number of channels
    /// </summary>
    int? Channels { get; set; }

    /// <summary>
    /// Sample rate in Hz
    /// </summary>
    int? SampleRate { get; set; }

    /// <summary>
    /// Quality level (codec-specific)
    /// </summary>
    int? Quality { get; set; }

    /// <summary>
    /// Creates a configured clone of this codec
    /// </summary>
    IAudioCodec Clone();
}

/// <summary>
/// Subtitle codec interface
/// </summary>
public interface ISubtitleCodec : ICodec
{
    /// <summary>
    /// Whether to burn subtitles into video
    /// </summary>
    bool BurnIn { get; set; }

    /// <summary>
    /// Creates a configured clone of this codec
    /// </summary>
    ISubtitleCodec Clone();
}

public enum CodecType
{
    Video,
    Audio,
    Subtitle
}

public enum HardwareAcceleration
{
    None,
    Nvenc,      // NVIDIA
    Qsv,        // Intel Quick Sync
    Vaapi,      // Linux VA-API
    VideoToolbox, // macOS
    Amf,        // AMD
    Dxva2,      // Windows DirectX
    Cuda        // NVIDIA CUDA
}

public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static ValidationResult Success() => new() { IsValid = true };

    public static ValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };

    public static ValidationResult WithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        Warnings = warnings
    };
}
