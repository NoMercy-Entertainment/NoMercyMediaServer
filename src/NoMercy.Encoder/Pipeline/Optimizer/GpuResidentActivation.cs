using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Output;

namespace NoMercy.Encoder.Pipeline.Optimizer;

/// <summary>
/// Composes the GPU-resident gate: opt-in flag, GPU presence, eligibility, the
/// no-thumbnails constraint (sprites need a CPU download), and the vendor scaler
/// being present in the running ffmpeg build. Returns the resolved
/// <see cref="GpuAccelPlan"/> or null for the CPU path. Pure so the whole
/// decision is unit-tested without a full PlanStage rig.
/// </summary>
public static class GpuResidentActivation
{
    public static GpuAccelPlan? Resolve(
        bool enabled,
        bool hasGpu,
        GpuVendor? vendor,
        OutputPlan plan,
        Func<string, bool> hasFilter
    )
    {
        if (!enabled || !hasGpu)
            return null;
        if (plan.Thumbnails is not null)
            return null;
        if (!GpuResidentEligibility.IsEligible(plan.VideoOutputs, plan.SubtitleOutputs))
            return null;

        return GpuAccelResolver.Resolve(vendor, hasFilter);
    }
}
