namespace NoMercy.Encoder.V3.Jobs;

public record JobCheckpoint(
    string JobId,
    string InputPath,
    string OutputDirectory,
    int[] CompletedGroupIndices,
    DateTime LastUpdated
);
