namespace NoMercy.Encoder.V3.Pipeline.Optimizer;

public record ExecutionGroup(
    string GroupId,
    ExecutionNode[] Nodes,
    string? DeviceId,
    int GpuSlotsRequired,
    int CpuThreadsRequired,
    bool RequiresGpu,
    int Priority
);
