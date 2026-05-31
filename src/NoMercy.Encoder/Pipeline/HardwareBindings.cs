namespace NoMercy.Encoder.Pipeline;

/// <summary>
/// Describes which GPU (if any) was allocated for this encode and which
/// FFmpeg decoder handle the pipeline will use. The dashboard surfaces
/// this in the job detail view so users can see whether encoding is
/// hardware-accelerated and which device is active. Populated by
/// <see cref="PlanResultProjector"/>; GpuBinding fills in during Phase 3
/// once ExecutionPlan carries hardware-binding information.
/// </summary>
public sealed record HardwareBindings(GpuBinding? PrimaryGpu, string? DecoderHandle);

/// <summary>
/// Identity of a single GPU selected for hardware-accelerated encoding.
/// </summary>
public sealed record GpuBinding(int Index, string Name, string Vendor);
