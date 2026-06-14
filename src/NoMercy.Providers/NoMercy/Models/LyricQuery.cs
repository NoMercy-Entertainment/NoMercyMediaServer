namespace NoMercy.Providers.NoMercy.Models;

/// <summary>
/// Normalized description of the local track we want lyrics for. Drives the
/// match scoring so a provider result is only accepted when it really is the
/// same song and (for synced lyrics) the same release length.
/// </summary>
public sealed record LyricQuery(
    string Title,
    IReadOnlyList<string> Artists,
    string? Album,
    int? DurationSeconds
);
