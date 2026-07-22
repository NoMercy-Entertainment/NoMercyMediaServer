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
/// Short episode forms that omit the full SxxExx tag: "E05", "Ep5", "Ep.05" and the
/// split "S01.E05" / "S2 E7" (season + separated episode). When no season token is
/// present the season defaults to 1. Restricted to anime/TV libraries so a movie's
/// stray "E"+number can never be claimed as an episode, and ordered after the
/// explicit SxxExx / cross-format / Episode-word matchers so they always win.
/// </summary>
public sealed partial class EpisodeShortFormAdapter : IFilenameParseAdapter
{
    public string Name => "episode-short-form";
    public int Order => 35;

    [GeneratedRegex(
        pattern: @"(?<![A-Za-z0-9])(?:S(\d{1,2})[\.\s\-_]?)?E(?:p|pisode)?[\.\s]?(\d{1,3})(?![A-Za-z0-9])",
        options: RegexOptions.IgnoreCase)]
    private static partial Regex ShortForm();

    public MovieFile? TryParse(ParseContext context)
    {
        if (
            context.LibraryType != MediaTypes.AnimeMediaType
            && context.LibraryType != MediaTypes.TvMediaType
        )
            return null;

        Match match = ShortForm().Match(input: context.CleanedFileName);
        if (!match.Success)
            return null;

        int season = match.Groups[groupnum: 1].Success ? int.Parse(s: match.Groups[groupnum: 1].Value) : 1;
        int episode = int.Parse(s: match.Groups[groupnum: 2].Value);

        string showTitle = context
            .CleanedFileName[..match.Index]
            .Replace(oldChar: '.', newChar: ' ')
            .Replace(oldChar: '_', newChar: ' ')
            .TrimEnd(trimChars: ['-', ' '])
            .Trim()
            .CleanSeriesTitle();

        if (string.IsNullOrWhiteSpace(value: showTitle) || showTitle.Length <= 1)
            showTitle = context.FolderTitle;

        return new(filePath: context.Title)
        {
            Title = showTitle,
            Season = season,
            Episode = episode,
            IsSeries = true,
            IsSuccess = true,
        };
    }
}
