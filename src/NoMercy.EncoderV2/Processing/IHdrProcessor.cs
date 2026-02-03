namespace NoMercy.EncoderV2.Processing;

/// <summary>
/// Supported HDR formats
/// </summary>
public enum HdrFormat
{
    /// <summary>Not HDR content</summary>
    None,
    /// <summary>HDR10 (PQ transfer, BT.2020 color space)</summary>
    Hdr10,
    /// <summary>HDR10+ (dynamic metadata)</summary>
    Hdr10Plus,
    /// <summary>Hybrid Log-Gamma</summary>
    Hlg,
    /// <summary>Dolby Vision</summary>
    DolbyVision
}

/// <summary>
/// Tone mapping algorithms for HDR to SDR conversion
/// </summary>
public enum ToneMappingAlgorithm
{
    /// <summary>Hable/Filmic tone mapping (default, good for most content)</summary>
    Hable,
    /// <summary>Reinhard tone mapping (preserves more detail in highlights)</summary>
    Reinhard,
    /// <summary>Mobius tone mapping (smooth rolloff)</summary>
    Mobius,
    /// <summary>BT.2390 tone mapping (ITU standard)</summary>
    Bt2390,
    /// <summary>Linear tone mapping (simple but can clip)</summary>
    Linear,
    /// <summary>No tone mapping - preserve HDR</summary>
    None
}

/// <summary>
/// Options for HDR processing
/// </summary>
public sealed class HdrProcessingOptions
{
    /// <summary>Tone mapping algorithm to use</summary>
    public ToneMappingAlgorithm Algorithm { get; init; } = ToneMappingAlgorithm.Hable;

    /// <summary>Peak brightness for input content (nits). 0 for auto-detection.</summary>
    public double PeakBrightness { get; init; } = 1000;

    /// <summary>Target peak brightness for SDR output (nits)</summary>
    public double TargetPeak { get; init; } = 100;

    /// <summary>Desaturation strength (0-1). Higher values reduce color saturation in bright areas.</summary>
    public double Desaturation { get; init; } = 0;

    /// <summary>Desat exponent for tone mapping curve</summary>
    public double DesatExponent { get; init; } = 1.5;

    /// <summary>Output pixel format</summary>
    public string OutputPixelFormat { get; init; } = "yuv420p";

    /// <summary>Apply color space conversion to BT.709</summary>
    public bool ConvertColorSpace { get; init; } = true;

    /// <summary>Preserve HDR metadata in output (when not converting to SDR)</summary>
    public bool PreserveMetadata { get; init; } = true;

    /// <summary>Hardware acceleration to use for processing</summary>
    public HdrHardwareAcceleration HardwareAcceleration { get; init; } = HdrHardwareAcceleration.None;
}

/// <summary>
/// Hardware acceleration options for HDR processing
/// </summary>
public enum HdrHardwareAcceleration
{
    /// <summary>Software processing (zscale/tonemap filters)</summary>
    None,
    /// <summary>NVIDIA CUDA with NPP</summary>
    Cuda,
    /// <summary>OpenCL acceleration</summary>
    OpenCl,
    /// <summary>Vulkan acceleration</summary>
    Vulkan
}

/// <summary>
/// Result of HDR detection
/// </summary>
public sealed class HdrDetectionResult
{
    /// <summary>Whether the content is HDR</summary>
    public bool IsHdr { get; init; }

    /// <summary>Detected HDR format</summary>
    public HdrFormat Format { get; init; }

    /// <summary>Color transfer characteristic (e.g., smpte2084, arib-std-b67)</summary>
    public string ColorTransfer { get; init; } = string.Empty;

    /// <summary>Color primaries (e.g., bt2020)</summary>
    public string ColorPrimaries { get; init; } = string.Empty;

    /// <summary>Color space/matrix (e.g., bt2020nc)</summary>
    public string ColorSpace { get; init; } = string.Empty;

    /// <summary>Maximum content light level (MaxCLL) in nits</summary>
    public int? MaxCll { get; init; }

    /// <summary>Maximum frame-average light level (MaxFALL) in nits</summary>
    public int? MaxFall { get; init; }

    /// <summary>Master display color volume metadata</summary>
    public string? MasterDisplayMetadata { get; init; }

    /// <summary>Whether Dolby Vision side data is present</summary>
    public bool HasDolbyVisionSideData { get; init; }
}

/// <summary>
/// Result of HDR to SDR conversion
/// </summary>
public sealed class HdrConversionResult
{
    /// <summary>Whether the conversion succeeded</summary>
    public bool Success { get; init; }

    /// <summary>Path to the converted SDR file</summary>
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>Detected HDR format of the source</summary>
    public HdrFormat SourceFormat { get; init; }

    /// <summary>Tone mapping algorithm used</summary>
    public ToneMappingAlgorithm AlgorithmUsed { get; init; }

    /// <summary>Processing duration</summary>
    public TimeSpan ProcessingTime { get; init; }

    /// <summary>Error message if conversion failed</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Interface for HDR detection and processing
/// </summary>
public interface IHdrProcessor
{
    /// <summary>
    /// Detects HDR format and metadata from a media file
    /// </summary>
    /// <param name="inputPath">Path to the input media file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HDR detection result with format and metadata</returns>
    Task<HdrDetectionResult> DetectHdrAsync(string inputPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts HDR content to SDR using tone mapping
    /// </summary>
    /// <param name="inputPath">Path to the HDR source file</param>
    /// <param name="outputPath">Path for the SDR output file</param>
    /// <param name="options">HDR processing options</param>
    /// <param name="progressCallback">Optional callback for progress updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conversion result with output path and metadata</returns>
    Task<HdrConversionResult> ConvertToSdrAsync(
        string inputPath,
        string outputPath,
        HdrProcessingOptions? options = null,
        Action<double>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the filter chain for HDR to SDR conversion
    /// </summary>
    /// <param name="sourceInfo">HDR detection result for the source</param>
    /// <param name="options">Processing options</param>
    /// <returns>FFmpeg filter string for tone mapping</returns>
    string BuildToneMappingFilterChain(HdrDetectionResult sourceInfo, HdrProcessingOptions? options = null);

    /// <summary>
    /// Checks if a cached SDR version exists for the given input
    /// </summary>
    /// <param name="inputPath">Path to the source HDR file</param>
    /// <param name="cacheDirectory">Directory to check for cached files</param>
    /// <returns>Path to cached file if exists, null otherwise</returns>
    string? GetCachedSdrPath(string inputPath, string cacheDirectory);

    /// <summary>
    /// Gets the recommended tone mapping algorithm for a given HDR format
    /// </summary>
    /// <param name="format">HDR format</param>
    /// <returns>Recommended tone mapping algorithm</returns>
    ToneMappingAlgorithm GetRecommendedAlgorithm(HdrFormat format);
}
