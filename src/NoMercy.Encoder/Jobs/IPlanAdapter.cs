namespace NoMercy.Encoder.Jobs;

using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline.Stages;

public interface IPlanAdapter
{
    ExecutionPlan AdaptForLocalHardware(
        ExecutionPlan originalPlan,
        IHardwareCapabilities localHardware,
        SpeedIndex localSpeeds
    );
}
