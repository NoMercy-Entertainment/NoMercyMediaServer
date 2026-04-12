namespace NoMercy.Encoder.V3.Jobs;

using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Pipeline.Stages;

public interface IPlanAdapter
{
    ExecutionPlan AdaptForLocalHardware(
        ExecutionPlan originalPlan,
        IHardwareCapabilities localHardware
    );
}
