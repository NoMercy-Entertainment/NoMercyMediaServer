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

using NoMercy.Providers.Abstractions;
using NoMercy.Providers.MusixMatch.Models;
using NoMercy.Providers.NoMercy.Models;

namespace NoMercy.Providers.MusixMatch.Client;

/// <summary>
/// Pulls the matched-track metadata and the timed subtitle body out of a
/// Musixmatch macro response so it can be validated against the requested track
/// before we trust it.
/// </summary>
public static class MusixMatchLyricMapper
{
    public static LyricCandidate? ToCandidate(MusixMatchSubtitleGet? response)
    {
        MusixMatchMacroCalls? macro = response?.Message?.Body?.MacroCalls;
        if (macro is null)
            return null;

        MusixMatchFormattedLyric[]? lines = macro
            .TrackSubtitlesGet?.Message?.Body?.SubtitleList?.FirstOrDefault()
            ?.Subtitle?.SubtitleBody;
        if (lines is null || lines.Length == 0)
            return null;

        MusixMatchMusixMatchTrack? track = macro
            .MatcherTrackGet
            ?.Message
            ?.Body
            ?.MusixMatchMusixMatchTrack;
        LyricLine[] mappedLines = lines
            .Select(line => new LyricLine
            {
                Text = line.Text,
                Time = new LyricLine.LineTime
                {
                    Total = line.Time.Total,
                    Minutes = line.Time.Minutes,
                    Seconds = line.Time.Seconds,
                    Hundredths = line.Time.Hundredths,
                },
            })
            .ToArray();

        bool hasSynced = lines.Any(line => line.Time.Total > 0);

        return new(
            track?.TrackName ?? string.Empty,
            track?.ArtistName ?? string.Empty,
            track is { TrackLength: > 0 } ? (int)track.TrackLength : null,
            hasSynced,
            mappedLines
        );
    }
}
