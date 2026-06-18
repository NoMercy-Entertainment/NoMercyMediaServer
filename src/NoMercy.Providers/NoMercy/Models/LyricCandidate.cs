using NoMercy.Providers.MusixMatch.Models;

namespace NoMercy.Providers.NoMercy.Models;

/// <summary>
/// A single lyrics result from any provider, reduced to the fields the matcher
/// needs to decide whether it belongs to the requested track.
/// </summary>
public sealed record LyricCandidate(
    string Title,
    string Artist,
    int? DurationSeconds,
    bool HasSyncedLyrics,
    MusixMatchFormattedLyric[] Lines
);
