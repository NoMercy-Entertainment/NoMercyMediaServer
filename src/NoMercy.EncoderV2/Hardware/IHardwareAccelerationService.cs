using NoMercy.NmSystem.Capabilities;

namespace NoMercy.EncoderV2.Hardware;

/// <summary>
/// Service for detecting and managing hardware acceleration
/// </summary>
public interface IHardwareAccelerationService
{
    /// <summary>
    /// Get all available GPU accelerators on the system
    /// </summary>
    List<GpuAccelerator> GetAvailableAccelerators();

    /// <summary>
    /// Get the best available accelerator for a given codec
    /// </summary>
    GpuAccelerator? GetBestAcceleratorForCodec(string codec);

    /// <summary>
    /// Check if hardware acceleration is available
    /// </summary>
    bool IsHardwareAccelerationAvailable();

    /// <summary>
    /// Get the recommended video codec based on available hardware
    /// </summary>
    string GetRecommendedVideoCodec(string requestedCodec);
}
