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
using NoMercy.NmSystem.Extensions;

namespace NoMercy.MediaProcessing.Files.Parsing.Adapters;

/// <summary>"Episode XX" pattern (e.g. "Blade - Episode 02 - title.mp4").</summary>
public sealed class EpisodeWordAdapter : IFilenameParseAdapter
{
    public string Name => "episode-word";
    public int Order => 20;

    public MovieFile? TryParse(ParseContext context)
    {
        string fileNameNoParens = StringExtensions
            .RemoveParenthesizedString()
            .Replace(input: context.CleanedFileName, replacement: string.Empty)
            .Trim();

        Match match = StringExtensions.MatchEpisodeWord().Match(input: fileNameNoParens);
        if (!match.Success)
            return null;

        int episodeNumber = int.Parse(s: match.Groups[groupnum: 1].Value);
        string showTitle = fileNameNoParens[..match.Index].TrimEnd(trimChars: ['-', '.', '_', ' ']);


        showTitle = showTitle.CleanSeriesTitle();

        if (string.IsNullOrWhiteSpace(value: showTitle) || showTitle.Length <= 1)
            showTitle = context.FolderTitle;

        return new(filePath: context.Title)
        {
            Title = showTitle,
            Season = 1,
            Episode = episodeNumber,
            IsSeries = true,
            IsSuccess = true,
        };
    }
}
