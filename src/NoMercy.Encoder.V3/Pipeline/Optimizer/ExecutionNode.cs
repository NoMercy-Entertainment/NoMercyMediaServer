namespace NoMercy.Encoder.V3.Pipeline.Optimizer;

using NoMercy.Encoder.V3.Hardware;

public record ExecutionNode(
    string Id,
    OperationType Operation,
    string[] DependsOn,
    Dictionary<string, string> Parameters,
    ResourceRequirement? Resource = null,
    string? DeviceId = null
);
