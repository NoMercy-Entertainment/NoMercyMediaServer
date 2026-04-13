namespace NoMercy.Encoder.Pipeline.Optimizer;

using NoMercy.Encoder.Hardware;

public record ExecutionNode(
    string Id,
    OperationType Operation,
    string[] DependsOn,
    Dictionary<string, string> Parameters,
    ResourceRequirement? Resource = null,
    string? DeviceId = null
);
