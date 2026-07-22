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
using System.Text.RegularExpressions;

namespace NoMercy.MediaProcessing.Files.Parsing;

/// <summary>
/// Expands a joined multi-episode file (e.g. "S01E01E02", "S01E01-E04",
/// "1x01-1x03") into the full list of episode numbers it covers, so a single file
/// spanning several episodes is not reported as missing episodes.
/// <para>
/// It augments an already-classified episode: the caller passes the season and
/// first episode the pipeline resolved, and this only extends them when the same
/// SxxExx / NxNN anchor in the name is followed by a repeat (E02E03) or a range
/// (-E04). The anchor is cross-checked against the supplied season/episode so a
/// title that merely looks like a number (e.g. the film "4x4") can never expand,
/// and bare resolution/codec digits are excluded because repeats must be
/// E-prefixed and ranges hyphen-delimited.
/// </para>
/// </summary>
public static partial class EpisodeRangeParser
{
    private const int MaxSpan = 50;

    [GeneratedRegex(
        pattern: @"(?<![A-Za-z0-9])S(\d{1,2})E(\d{1,4})((?:[\s._]*E\d{1,4})*)(?:[\s._-]*-[\s._-]*E?(\d{1,4}))?",
        options: RegexOptions.IgnoreCase)]
    private static partial Regex SeasonEpisodeRange();

    [GeneratedRegex(
        pattern: @"(?<![A-Za-z0-9])(\d{1,2})x(\d{1,3})((?:x\d{1,3})*)(?:[\s._-]*-[\s._-]*(?:\d{1,2}x)?(\d{1,3}))?",
        options: RegexOptions.IgnoreCase)]
    private static partial Regex CrossEpisodeRange();

    [GeneratedRegex(pattern: @"\d{1,4}")]
    private static partial Regex Numbers();

    /// <summary>
    /// Returns the ordered episode numbers covered by <paramref name="name"/> for
    /// the given <paramref name="season"/>/<paramref name="firstEpisode"/>. When no
    /// range/repeat is present (or the anchor does not match), the single supplied
    /// episode is returned unchanged.
    /// </summary>
    public static IReadOnlyList<int> Expand(string? name, int season, int firstEpisode)
    {
        if (string.IsNullOrEmpty(value: name))
            return [firstEpisode];

        Match m = SeasonEpisodeRange().Match(input: name);
        if (m.Success
            && int.Parse(s: m.Groups[groupnum: 1].Value) == season
            && int.Parse(s: m.Groups[groupnum: 2].Value) == firstEpisode)
            return Build(first: firstEpisode, repeats: m.Groups[groupnum: 3].Value, rangeEnd: m.Groups[groupnum: 4]);

        Match c = CrossEpisodeRange().Match(input: name);
        if (c.Success
            && int.Parse(s: c.Groups[groupnum: 1].Value) == season
            && int.Parse(s: c.Groups[groupnum: 2].Value) == firstEpisode)
            return Build(first: firstEpisode, repeats: c.Groups[groupnum: 3].Value, rangeEnd: c.Groups[groupnum: 4]);

        return [firstEpisode];
    }

    private static IReadOnlyList<int> Build(int first, string repeats, Group rangeEnd)
    {
        if (rangeEnd.Success)
        {
            int last = int.Parse(s: rangeEnd.Value);
            return last > first && last - first <= MaxSpan
                ? [.. Enumerable.Range(start: first, count: last - first + 1)]
                : [first];
        }

        SortedSet<int> episodes = [first];
        foreach (Match n in Numbers().Matches(input: repeats))
            episodes.Add(item: int.Parse(s: n.Value));

        return episodes.Max - episodes.Min > MaxSpan ? [first] : [.. episodes];
    }
}
