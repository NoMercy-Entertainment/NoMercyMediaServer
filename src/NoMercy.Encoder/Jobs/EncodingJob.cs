using NoMercy.Encoder.Profiles.V2;

namespace NoMercy.Encoder.Jobs;

public record EncodingJob(
    string JobId,
    string InputPath,
    string OutputDirectory,
    EncodingProfile Profile,
    JobCheckpoint? Checkpoint,
    DateTime CreatedAtUtc,
    string? HmacSignature = null
);
