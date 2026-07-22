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

/// <summary>Cross-format numbering: "1x05", "12x08", "1×05" (SeasonxEpisode).
/// Resolution tokens like 1920x1080 are excluded because the digits before the
/// separator are preceded by another digit.</summary>
public sealed class CrossFormatAdapter : IFilenameParseAdapter
{
    public string Name => "cross-format";
    public int Order => 25;

    public MovieFile? TryParse(ParseContext context)
    {
        Match match = StringExtensions.MatchCrossFormatEpisode().Match(input: context.CleanedFileName);
        if (!match.Success)
            return null;

        // A cross-format match at the very start is the title itself (e.g. the
        // film "4x4"); a genuine SxExx tag is always preceded by the show title.
        if (match.Index == 0)
            return null;

        string showTitle = context
            .CleanedFileName[..match.Index]
            .Replace(oldChar: '.', newChar: ' ')
            .Replace(oldChar: '_', newChar: ' ')
            .TrimEnd(trimChars: ['-', ' '])
            .Trim();

        showTitle = showTitle.CleanSeriesTitle();

        if (string.IsNullOrWhiteSpace(value: showTitle) || showTitle.Length <= 1)
            showTitle = context.FolderTitle;

        return new(filePath: context.Title)
        {
            Title = showTitle,
            Season = int.Parse(s: match.Groups[groupnum: 1].Value),
            Episode = int.Parse(s: match.Groups[groupnum: 2].Value),
            IsSeries = true,
            IsSuccess = true,
        };
    }
}
