namespace NoMercy.Encoder.V3.Jobs;

using NoMercy.Encoder.V3.Profiles;

public record EncodingJob(
    string JobId,
    string InputPath,
    string OutputDirectory,
    EncodingProfile Profile,
    JobCheckpoint? Checkpoint
);
