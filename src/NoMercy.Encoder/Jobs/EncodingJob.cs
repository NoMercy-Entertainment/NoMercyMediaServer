namespace NoMercy.Encoder.Jobs;

using NoMercy.Encoder.Profiles;

public record EncodingJob(
    string JobId,
    string InputPath,
    string OutputDirectory,
    EncodingProfile Profile,
    JobCheckpoint? Checkpoint,
    DateTime CreatedAtUtc,
    string? HmacSignature = null
);
