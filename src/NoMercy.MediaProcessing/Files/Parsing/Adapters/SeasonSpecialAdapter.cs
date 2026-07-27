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
using MovieFileLibrary;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.MediaProcessing.Files.Parsing.Adapters;

/// <summary>
/// Season-scoped specials written as <c>S##S##</c> — "season 1's special 3" —
/// which is how Judas, Erai-raws and most BD batch releases number the extras
/// that ship alongside a season. Maps to season 0, keeping the special's own
/// number as the episode.
/// <para>
/// Restricted to anime/TV libraries and ordered directly after the S##E##
/// matcher, whose marker it deliberately cannot match: without it these names
/// reached the movie detector, came back with neither season nor episode, and
/// the caller's last-resort "first number in the title" rule then read the
/// SEASON digits as the episode — so every special in a season arrived as
/// S##E01, each one overwriting the last.
/// </para>
/// </summary>
public sealed partial class SeasonSpecialAdapter : IFilenameParseAdapter
{
    public string Name => "season-special";
    public int Order => 32;

    [GeneratedRegex(
        @"(?<![A-Za-z0-9])S(?<season>\d{1,2})[\s\.\-_]*S(?<special>\d{1,3})(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex SeasonSpecialMarker();

    public MovieFile? TryParse(ParseContext context)
    {
        if (
            context.LibraryType != MediaTypes.AnimeMediaType
            && context.LibraryType != MediaTypes.TvMediaType
        )
            return null;

        Match match = SeasonSpecialMarker().Match(context.CleanedFileName);
        if (!match.Success)
            return null;

        string title = context
            .CleanedFileName[..match.Index]
            .Replace('.', ' ')
            .Replace('_', ' ')
            .TrimEnd('-', ' ')
            .Trim()
            .CleanSeriesTitle();

        if (string.IsNullOrWhiteSpace(title) || title.Length <= 1)
            title = context.FolderTitle;

        if (string.IsNullOrWhiteSpace(title) || title.Length <= 1)
            return null;

        return new(context.Title)
        {
            Title = title,
            Season = 0,
            Episode = int.Parse(match.Groups["special"].Value),
            IsSeries = true,
            IsSuccess = true,
        };
    }
}
