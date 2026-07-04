// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.NoMercy.Models;

namespace NoMercy.Providers.NoMercy.Client;

/// <summary>
/// Scores provider lyric results against the requested track so we never store
/// lyrics from a different song, and so synced lyrics only come from a release
/// whose length matches the local file (otherwise every line is time-shifted).
/// </summary>
public static class LyricMatcher
{
    private const double TitleThreshold = 80.0;
    private const double ArtistThreshold = 70.0;

    // Different releases (live, extended, remaster with a longer intro) drift the
    // synced timestamps. Beyond this gap a synced match is rejected rather than
    // shown at the wrong moment.
    private const int SyncedDurationToleranceSeconds = 8;

    public static LyricCandidate? PickBest(LyricQuery query, IEnumerable<LyricCandidate> candidates)
    {
        LyricCandidate? best = null;
        double bestScore = double.NegativeInfinity;

        foreach (LyricCandidate candidate in candidates)
        {
            double score = Score(query, candidate);
            if (score < 0)
                continue;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Returns a 0+ score, or a negative number when the candidate is not an
    /// acceptable match and must be discarded.
    /// </summary>
    public static double Score(LyricQuery query, LyricCandidate candidate)
    {
        if (candidate.Lines.Length == 0)
            return -1;

        double titleScore = TitleMatch(query.Title, candidate.Title);
        if (titleScore < TitleThreshold)
            return -1;

        double artistScore = ArtistMatch(query.Artists, candidate.Artist);
        if (artistScore < ArtistThreshold)
            return -1;

        double durationScore = 100.0;
        if (query.DurationSeconds is { } wanted && candidate.DurationSeconds is { } got)
        {
            int diff = Math.Abs(wanted - got);
            if (candidate.HasSyncedLyrics && diff > SyncedDurationToleranceSeconds)
                return -1;
            durationScore = Math.Max(0, 100.0 - (diff * 5.0));
        }

        double syncBonus = candidate.HasSyncedLyrics ? 20.0 : 0.0;
        return (titleScore * 0.4) + (artistScore * 0.35) + (durationScore * 0.25) + syncBonus;
    }

    private static double TitleMatch(string wanted, string got)
    {
        string a = wanted.NormalizeForComparison();
        string b = got.NormalizeForComparison();
        if (a.Length == 0 || b.Length == 0)
            return 0;
        if (a == b)
            return 100;
        if (a.Contains(b) || b.Contains(a))
            return 95;
        return FuzzyMatcher.MatchPercentage(a, b);
    }

    private static double ArtistMatch(IReadOnlyList<string> wantedArtists, string got)
    {
        string b = got.NormalizeForComparison();
        if (b.Length == 0)
            return 0;

        double best = 0;
        foreach (string wanted in wantedArtists)
        {
            string a = wanted.NormalizeForComparison();
            if (a.Length == 0)
                continue;

            double score =
                a == b ? 100
                : b.Contains(a) || a.Contains(b) ? 95
                : FuzzyMatcher.MatchPercentage(a, b);
            if (score > best)
                best = score;
        }

        return best;
    }
}
