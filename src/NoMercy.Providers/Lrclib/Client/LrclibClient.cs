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

using System.Globalization;
using System.Text.RegularExpressions;
using NoMercy.Providers.Lrclib.Models;
using NoMercy.Providers.MusixMatch.Models;
using NoMercy.Providers.NoMercy.Models;

namespace NoMercy.Providers.Lrclib.Client;

public partial class LrclibClient : LrclibBaseClient
{
    /// <summary>
    /// Exact signature lookup (<c>/api/get</c>). Returns a single release matched
    /// on artist + track + album + duration, or null when none exists.
    /// </summary>
    public async Task<LrclibSongResult?> Get(
        string[] artists,
        string trackName,
        string? albumName = null,
        double? duration = null,
        bool priority = false
    )
    {
        Dictionary<string, string?> additionalArguments = new()
        {
            { "artist_name", string.Join(",", artists) },
            { "track_name", trackName },
        };
        if (albumName != null)
            additionalArguments.Add("album_name", albumName);
        if (duration.HasValue)
            additionalArguments.Add(
                "duration",
                duration.Value.ToString(CultureInfo.InvariantCulture)
            );

        LrclibSongResult? result = await Get<LrclibSongResult>(
            "get",
            additionalArguments,
            priority
        );
        if (
            !string.IsNullOrEmpty(result?.Message)
            || result?.StatusCode != 200
            || result.Name == "TrackNotFound"
        )
            return null;
        return result;
    }

    /// <summary>
    /// Fuzzy search (<c>/api/search</c>). Returns every candidate release so the
    /// caller can score them and pick the one whose duration matches the local
    /// file, instead of blindly taking the first hit.
    /// </summary>
    public async Task<LrclibSongResult[]?> Search(
        string[] artists,
        string trackName,
        string? albumName = null,
        bool priority = false
    )
    {
        Dictionary<string, string?> additionalArguments = new() { { "track_name", trackName } };
        string artistName = string.Join(",", artists);
        if (!string.IsNullOrEmpty(artistName))
            additionalArguments.Add("artist_name", artistName);
        if (albumName != null)
            additionalArguments.Add("album_name", albumName);

        return await Get<LrclibSongResult[]>("search", additionalArguments, priority);
    }

    /// <summary>
    /// Reduces a raw Lrclib release to a scoreable candidate, preferring synced
    /// lyrics over plain. Returns null when the release has no usable lyrics.
    /// </summary>
    public static LyricCandidate? ToCandidate(LrclibSongResult result)
    {
        if (result.Instrumental)
            return null;

        bool hasSynced = !string.IsNullOrEmpty(result.SyncedLyrics);
        MusixMatchFormattedLyric[]? lines = ConvertToMusixmatchLyrics(
            hasSynced ? result.SyncedLyrics : result.PlainLyrics
        );
        if (lines is null)
            return null;

        return new(
            result.TrackName,
            result.ArtistName,
            result.Duration > 0 ? (int)Math.Round(result.Duration) : null,
            hasSynced,
            lines
        );
    }

    private static MusixMatchFormattedLyric[]? ConvertToMusixmatchLyrics(string? lyrics)
    {
        if (string.IsNullOrEmpty(lyrics))
            return null;
        string[] lines = lyrics.Split(['\r', '\n'], StringSplitOptions.None);

        List<MusixMatchFormattedLyric> lyricLines = [];
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;
            MusixMatchFormattedLyric? lyricLine = MakeLyricLine(trimmedLine);
            if (lyricLine != null)
                lyricLines.Add(lyricLine);
        }
        return lyricLines.Count == 0 ? null : lyricLines.ToArray();
    }

    private static MusixMatchFormattedLyric? MakeLyricLine(string trimmedLine)
    {
        if (string.IsNullOrEmpty(trimmedLine))
            return null;

        Match match = TimeStamped().Match(trimmedLine);
        if (!match.Success)
            return new()
            {
                Text = trimmedLine,
                Time = new()
                {
                    Total = 0,
                    Minutes = 0,
                    Seconds = 0,
                    Hundredths = 0,
                },
            };

        int minutes = int.Parse(match.Groups[1].Value);
        int seconds = int.Parse(match.Groups[2].Value);
        int hundredths = int.Parse(match.Groups[3].Value);
        string text = match.Groups[4].Value.Trim();
        double total = (minutes * 60) + seconds + (hundredths / 100.0);

        return new()
        {
            Text = text,
            Time = new()
            {
                Total = total,
                Minutes = minutes,
                Seconds = seconds,
                Hundredths = hundredths,
            },
        };
    }

    [GeneratedRegex(@"\[(\d{2}):(\d{2})\.(\d{2})\](.*)$")]
    private static partial Regex TimeStamped();
}
