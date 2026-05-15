using NoMercy.Encoder.Output;
using NoMercy.Resources;

namespace NoMercy.Encoder.Strategies.Shared;

/// <summary>
/// Derives <see cref="ResourceRequirement"/> for a decomposed task from the
/// output plan. GPU is detected by encoder name suffix — any encoder whose
/// name contains a hardware-acceleration token (nvenc, amf, qsv, vaapi,
/// videotoolbox, cuvid) gets a GPU-slot requirement. All other tasks get
/// CPU-only requirements.
/// </summary>
internal static class TaskResourceHelper
{
    private static readonly string[] GpuEncoderTokens =
    [
        "nvenc",
        "amf",
        "qsv",
        "vaapi",
        "videotoolbox",
        "cuvid",
    ];

    public static ResourceRequirement ForVideoOutput(VideoOutputPlan video)
    {
        if (IsGpuEncoder(video.EncoderName))
            return new ResourceRequirement(video.EncoderName, GpuSlots: 1, CpuThreads: 2);

        int cpuThreads = Math.Max(1, Environment.ProcessorCount / 2);
        return new ResourceRequirement(null, GpuSlots: 0, CpuThreads: cpuThreads);
    }

    public static ResourceRequirement CpuOnly(int cpuThreads = 1) =>
        new(null, GpuSlots: 0, CpuThreads: cpuThreads);

    private static bool IsGpuEncoder(string encoderName)
    {
        if (string.IsNullOrEmpty(encoderName))
            return false;

        string lower = encoderName.ToLowerInvariant();
        foreach (string token in GpuEncoderTokens)
        {
            if (lower.Contains(token, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
