namespace NoMercy.Encoder.V3.Pipeline.Optimizer;

public record CostEstimate(
    TimeSpan EstimatedDuration,
    double GpuUtilization,
    double CpuUtilization,
    long EstimatedOutputBytes
);

public class CostEstimator
{
    public CostEstimate EstimateGroup(ExecutionGroup group, TimeSpan inputDuration)
    {
        bool hasVideoEncode = group.Nodes.Any(n => n.Operation == OperationType.Encode);

        if (!hasVideoEncode)
        {
            // Subtitle/chapter/thumbnail extraction is nearly instant
            return new CostEstimate(
                EstimatedDuration: TimeSpan.FromSeconds(10),
                GpuUtilization: 0,
                CpuUtilization: 0.1,
                EstimatedOutputBytes: 0
            );
        }

        // Base estimate: input duration / speed factor
        // GPU groups: assume 2.5x realtime  (faster-than-realtime)
        // CPU groups: assume 0.8x realtime  (slower-than-realtime)
        double speedFactor = group.RequiresGpu ? 2.5 : 0.8;

        TimeSpan estimated = TimeSpan.FromSeconds(inputDuration.TotalSeconds / speedFactor);

        // GPU utilization as fraction of max encoder sessions (assume 12 slots as reference)
        double gpuUtil = group.RequiresGpu ? (double)group.GpuSlotsRequired / 12 : 0;

        return new CostEstimate(
            EstimatedDuration: estimated,
            GpuUtilization: gpuUtil,
            CpuUtilization: group.RequiresGpu ? 0.2 : 0.8,
            EstimatedOutputBytes: 0
        );
    }

    public TimeSpan EstimateTotal(List<ExecutionGroup> groups, TimeSpan inputDuration)
    {
        TimeSpan total = TimeSpan.Zero;
        foreach (ExecutionGroup group in groups)
        {
            CostEstimate estimate = EstimateGroup(group, inputDuration);
            total += estimate.EstimatedDuration;
        }

        return total;
    }
}
