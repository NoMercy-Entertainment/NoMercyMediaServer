namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Enforces the NVIDIA concurrent NVENC session limit at dispatch time.
/// Consumer NVIDIA GPUs historically allow at most 3 simultaneous NVENC
/// sessions; professional cards have no effective cap. The limit is read
/// from <see cref="GpuDevice.MaxEncoderSessions"/> so it can be overridden
/// per-device when hardware capabilities are detected.
/// </summary>
public interface INvencSessionCap
{
    /// <summary>
    /// Throws <see cref="NoMercy.Encoder.Errors.EncoderRuntimeException"/> (HTTP 409)
    /// when every NVENC slot on every NVIDIA GPU is occupied and
    /// <paramref name="requiresGpu"/> is <c>true</c>. When
    /// <paramref name="requiresGpu"/> is <c>false</c> the method is a no-op
    /// so callers for software encodes skip the check with a single flag.
    /// </summary>
    /// <param name="gpuName">
    /// Display name of the target GPU, used in the error message.
    /// Pass the name from <see cref="GpuDevice.Name"/>.
    /// </param>
    /// <param name="requiresGpu">
    /// <c>true</c> when the profile uses hardware encoding (any
    /// <see cref="NoMercy.Encoder.Profiles.HardwarePreference"/> other than
    /// <c>ForceSoftware</c> or <c>PreferQuality</c>). <c>false</c> skips
    /// the cap check entirely.
    /// </param>
    void EnforceForGpuEncode(string gpuName, bool requiresGpu = true);
}
