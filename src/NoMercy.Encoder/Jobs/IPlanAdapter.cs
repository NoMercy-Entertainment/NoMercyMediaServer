using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline.Stages;

namespace NoMercy.Encoder.Jobs;

public interface IPlanAdapter
{
    ExecutionPlan AdaptForLocalHardware(
        ExecutionPlan originalPlan,
        IHardwareCapabilities localHardware,
        SpeedIndex localSpeeds
    );
}
