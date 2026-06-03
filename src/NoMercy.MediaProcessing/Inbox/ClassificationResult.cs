using NoMercy.Database.Models.Libraries;

namespace NoMercy.MediaProcessing.Inbox;

public sealed class ClassificationResult
{
    public required string DetectedType { get; init; }

    public required string Confidence { get; init; }

    public required CandidateMatch[] Candidates { get; init; }
}
