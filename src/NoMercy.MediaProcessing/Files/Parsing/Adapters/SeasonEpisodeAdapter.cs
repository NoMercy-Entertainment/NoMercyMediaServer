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

/// <summary>S##E## (or S##E####) anywhere in the file name (e.g.
/// "One.Piece.S01E1109.Title.mkv"); the show title is the text before the match.</summary>
public sealed class SeasonEpisodeAdapter : IFilenameParseAdapter
{
    public string Name => "season-episode";
    public int Order => 30;

    public MovieFile? TryParse(ParseContext context)
    {
        Match match = StringExtensions.MatchSeasonEpisode().Match(context.CleanedFileName);
        if (!match.Success)
            return null;

        string showTitle = context
            .CleanedFileName[..match.Index]
            .Replace('.', ' ')
            .Replace('_', ' ')
            .TrimEnd('-', ' ')
            .Trim();

        Match yearInTitle = StringExtensions.MatchYearRegex().Match(showTitle);
        if (yearInTitle.Success)
            showTitle = showTitle[..yearInTitle.Index].TrimEnd('-', '.', '_', ' ');

        if (string.IsNullOrWhiteSpace(showTitle) || showTitle.Length <= 1)
            showTitle = context.FolderTitle;

        return new(context.Title)
        {
            Title = showTitle,
            Season = int.Parse(match.Groups[1].Value),
            Episode = int.Parse(match.Groups[2].Value),
            IsSeries = true,
            IsSuccess = true,
        };
    }
}
