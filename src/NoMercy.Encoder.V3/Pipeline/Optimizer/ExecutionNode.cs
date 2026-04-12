namespace NoMercy.Encoder.V3.Pipeline.Optimizer;

public record ExecutionNode(
    string Id,
    OperationType Operation,
    string[] DependsOn,
    Dictionary<string, string> Parameters
);
