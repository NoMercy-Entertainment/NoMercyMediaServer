using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;

namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Production implementation of <see cref="INvencSessionCap"/>.
/// Compares <see cref="IEncoderProcessRegistry.CountConcurrentNvencSessions"/>
/// against the lowest <see cref="GpuDevice.MaxEncoderSessions"/> across all
/// detected NVIDIA GPUs and throws when the cap is reached.
/// </summary>
public sealed class NvencSessionCap(
    IHardwareCapabilities hardware,
    IEncoderProcessRegistry registry
) : INvencSessionCap
{
    public void EnforceForGpuEncode(string gpuName, bool requiresGpu = true)
    {
        if (!requiresGpu)
            return;

        if (!hardware.HasGpu || hardware.Gpus.Count == 0)
            return;

        // Use the most restrictive cap across all GPUs. A multi-GPU system with
        // one consumer card (cap 3) and one pro card (cap int.MaxValue) should
        // respect the consumer card's limit because the session could land on it.
        int effectiveCap = hardware.Gpus.Min(g => g.MaxEncoderSessions);

        // No cap means the hardware reports unlimited sessions (pro cards).
        if (effectiveCap <= 0 || effectiveCap == int.MaxValue)
            return;

        int active = registry.CountConcurrentNvencSessions();
        if (active >= effectiveCap)
        {
            throw RuntimeErrors.GpuCapacityExhausted(
                gpuName,
                active,
                "Wait for an active encode to free a slot, or set hardware_preference=force_software on this profile to use CPU instead."
            );
        }
    }
}
