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
using NoMercy.Providers.Abstractions;
using NoMercy.Providers.Lrclib.Models;
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
            { "artist_name", string.Join(separator: ",", value: artists) },
            { "track_name", trackName },
        };
        if (albumName != null)
            additionalArguments.Add(key: "album_name", value: albumName);
        if (duration.HasValue)
            additionalArguments.Add(
                key: "duration",
                value: duration.Value.ToString(provider: CultureInfo.InvariantCulture)
            );

        LrclibSongResult? result = await Get<LrclibSongResult>(
            url: "get",
            query: additionalArguments,
            priority: priority
        );
        if (
            !string.IsNullOrEmpty(value: result?.Message)
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
        string artistName = string.Join(separator: ",", value: artists);
        if (!string.IsNullOrEmpty(value: artistName))
            additionalArguments.Add(key: "artist_name", value: artistName);
        if (albumName != null)
            additionalArguments.Add(key: "album_name", value: albumName);

        return await Get<LrclibSongResult[]>(url: "search", query: additionalArguments, priority: priority);
    }

    /// <summary>
    /// Reduces a raw Lrclib release to a scoreable candidate, preferring synced
    /// lyrics over plain. Returns null when the release has no usable lyrics.
    /// </summary>
    public static LyricCandidate? ToCandidate(LrclibSongResult result)
    {
        if (result.Instrumental)
            return null;

        bool hasSynced = !string.IsNullOrEmpty(value: result.SyncedLyrics);
        LyricLine[]? lines = ConvertToMusixmatchLyrics(
            lyrics: hasSynced ? result.SyncedLyrics : result.PlainLyrics
        );
        if (lines is null)
            return null;

        return new(
            Title: result.TrackName,
            Artist: result.ArtistName,
            DurationSeconds: result.Duration > 0 ? (int)Math.Round(a: result.Duration) : null,
            HasSyncedLyrics: hasSynced,
            Lines: lines
        );
    }

    private static LyricLine[]? ConvertToMusixmatchLyrics(string? lyrics)
    {
        if (string.IsNullOrEmpty(value: lyrics))
            return null;
        string[] lines = lyrics.Split(separator: ['\r', '\n'], options: StringSplitOptions.None);

        List<LyricLine> lyricLines = [];
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(value: trimmedLine))
                continue;
            LyricLine? lyricLine = MakeLyricLine(trimmedLine: trimmedLine);
            if (lyricLine != null)
                lyricLines.Add(item: lyricLine);
        }
        return lyricLines.Count == 0 ? null : lyricLines.ToArray();
    }

    private static LyricLine? MakeLyricLine(string trimmedLine)
    {
        if (string.IsNullOrEmpty(value: trimmedLine))
            return null;

        Match match = TimeStamped().Match(input: trimmedLine);
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

        int minutes = int.Parse(s: match.Groups[groupnum: 1].Value);
        int seconds = int.Parse(s: match.Groups[groupnum: 2].Value);
        // LRC fractions come in both hundredths ([mm:ss.xx]) and milliseconds
        // ([mm:ss.xxx]) precision. Scale by the digit count so a 3-digit tag is
        // read as milliseconds, not as an out-of-range hundredths value — and so
        // the timestamp is stripped from the text instead of being left in it.
        string fraction = match.Groups[groupnum: 3].Value;
        double fractionalSeconds = fraction.Length switch
        {
            3 => int.Parse(s: fraction) / 1000.0,
            2 => int.Parse(s: fraction) / 100.0,
            _ => 0,
        };
        string text = match.Groups[groupnum: 4].Value.Trim();
        double total = (minutes * 60) + seconds + fractionalSeconds;

        return new()
        {
            Text = text,
            Time = new()
            {
                Total = total,
                Minutes = minutes,
                Seconds = seconds,
                Hundredths = (int)Math.Round(a: fractionalSeconds * 100),
            },
        };
    }

    [GeneratedRegex(pattern: @"^\[(\d{1,2}):(\d{2})(?:[.:](\d{2,3}))?\](.*)$")]
    private static partial Regex TimeStamped();
}
