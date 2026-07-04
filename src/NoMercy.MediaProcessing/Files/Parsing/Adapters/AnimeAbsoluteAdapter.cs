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
/// Anime absolute numbering, e.g. "[Group] One Piece - 1109 [1080p]" -> episode 1109.
/// Active only for anime/TV libraries so it can never claim a movie's year as an
/// episode. Years and resolution tokens are excluded, and it is ordered after the
/// SxxExx / cross-format adapters so explicit numbering always wins. Season is left
/// unset so the absolute-episode resolver downstream is allowed to run.
/// </summary>
public sealed partial class AnimeAbsoluteAdapter : IFilenameParseAdapter
{
    public string Name => "anime-absolute";
    public int Order => 50;

    [GeneratedRegex(@"(?<![A-Za-z0-9])(\d{1,4})(?![A-Za-z0-9])")]
    private static partial Regex StandaloneNumber();

    public MovieFile? TryParse(ParseContext context)
    {
        if (
            context.LibraryType != MediaTypes.AnimeMediaType
            && context.LibraryType != MediaTypes.TvMediaType
        )
            return null;

        MatchCollection matches = StandaloneNumber().Matches(context.CleanedFileName);
        Match? episode = null;
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            if (LooksLikeYear(matches[i].Groups[1].Value))
                continue;
            episode = matches[i];
            break;
        }

        if (episode is null)
            return null;

        string showTitle = context
            .CleanedFileName[..episode.Index]
            .Replace('.', ' ')
            .Replace('_', ' ')
            .TrimEnd('-', ' ')
            .Trim();

        showTitle = showTitle.CleanSeriesTitle();

        if (string.IsNullOrWhiteSpace(showTitle) || showTitle.Length <= 1)
            showTitle = context.FolderTitle;

        return new(context.Title)
        {
            Title = showTitle,
            Episode = int.Parse(episode.Groups[1].Value),
            IsSeries = true,
            IsSuccess = true,
        };
    }

    private static bool LooksLikeYear(string number) =>
        number.Length == 4
        && (number.StartsWith("18") || number.StartsWith("19") || number.StartsWith("20"));
}
