namespace NoMercy.Encoder.Jobs;

public record JobCheckpoint(
    string JobId,
    string InputPath,
    string OutputDirectory,
    int[] CompletedGroupIndices,
    DateTime LastUpdated,
    string? StatsFilePath = null,
    bool Pass1Completed = false,
    int LastCompletedSegment = -1,
    string? EncodeMode = null
);
