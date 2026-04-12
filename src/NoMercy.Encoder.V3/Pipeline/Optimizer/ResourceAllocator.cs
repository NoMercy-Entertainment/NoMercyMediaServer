namespace NoMercy.Encoder.V3.Pipeline.Optimizer;

using NoMercy.Encoder.V3.Hardware;

public class ResourceAllocator(IHardwareCapabilities hardware)
{
    public void AllocateResources(List<ExecutionGroup> groups)
    {
        foreach (ExecutionGroup group in groups)
        {
            if (group.RequiresGpu && hardware.HasGpu)
            {
                // Assign to least-loaded GPU (for now, first GPU)
                // Future: round-robin or load-based for multi-GPU
            }

            // CPU thread budget for software encodes
            if (!group.RequiresGpu && group.CpuThreadsRequired == 0)
            {
                // Assign reasonable thread count
                // Default: half available cores per software encode group
            }
        }
    }

    public bool CheckMemoryCeiling(List<ExecutionGroup> groups, long availableMemoryMb)
    {
        // Estimate peak memory per group — ~200MB per active encode stream
        long estimatedPeakMb = 0;
        foreach (ExecutionGroup group in groups)
        {
            int encodeCount = group.Nodes.Count(n => n.Operation == OperationType.Encode);
            estimatedPeakMb += encodeCount * 200;
        }

        return estimatedPeakMb < availableMemoryMb * 75 / 100;
    }
}
