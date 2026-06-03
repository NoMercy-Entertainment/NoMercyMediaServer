using NoMercy.OpticalMedia.Metadata;

namespace NoMercy.OpticalMedia.Rip;

/// <summary>
/// Written as a JSON sidecar (<c>{outputDir}/pending_{titleIndex:D2}.json</c>)
/// when a rip completes but the top TMDB candidate's confidence falls below
/// the auto-apply threshold. The dashboard reads these files to surface
/// "awaiting manual confirmation" entries for the user to resolve.
/// </summary>
public sealed record DiscRipPendingState(
    string RipOutputPath,
    int TitleIndex,
    string DrivePath,
    int DiscDurationSec,
    DiscCandidate[] Candidates,
    DateTimeOffset CreatedAt
);
