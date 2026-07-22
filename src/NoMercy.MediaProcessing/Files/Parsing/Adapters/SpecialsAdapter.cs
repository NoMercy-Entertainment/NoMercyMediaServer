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
/// Specials / season-zero content that is labelled rather than numbered: anime
/// markers (OVA, ONA, OAD, SP##, NCOP, NCED) and the words "Special(s)"/"Extra(s)".
/// Maps to season 0; the trailing number (if any) becomes the episode, else 1.
/// <para>
/// Restricted to anime/TV libraries and ordered after every explicit episode
/// matcher, so a real "S00E05" or "Special.S01E01" is already claimed upstream.
/// Token boundaries are non-alphanumeric so substrings inside real titles
/// ("Nova", "Casanova", "Spectre", "Extraction", "Specialist") never match, and
/// the ambiguous WORD markers additionally require a show title to precede them in
/// the file name itself - protecting shows literally named "Special" or "Extras".
/// </para>
/// </summary>
public sealed partial class SpecialsAdapter : IFilenameParseAdapter
{
    public string Name => "specials";
    public int Order => 40;

    [GeneratedRegex(
        pattern: @"(?<![A-Za-z0-9])(?:(?<word>specials?|extras?)|(?<anime>ova|ona|oad|ncop|nced|sp))(?:[\s\.\-_]*(?<num>\d{1,3}))?(?![A-Za-z0-9])",
        options: RegexOptions.IgnoreCase)]
    private static partial Regex SpecialMarker();

    public MovieFile? TryParse(ParseContext context)
    {
        if (
            context.LibraryType != MediaTypes.AnimeMediaType
            && context.LibraryType != MediaTypes.TvMediaType
        )
            return null;

        Match match = SpecialMarker().Match(input: context.CleanedFileName);
        if (!match.Success)
            return null;

        string title = context
            .CleanedFileName[..match.Index]
            .Replace(oldChar: '.', newChar: ' ')
            .Replace(oldChar: '_', newChar: ' ')
            .TrimEnd(trimChars: ['-', ' '])
            .Trim()
            .CleanSeriesTitle();

        bool isWordMarker = match.Groups[groupname: "word"].Success;

        if (string.IsNullOrWhiteSpace(value: title) || title.Length <= 1)
        {
            // Word markers double as real show titles; only the unambiguous anime
            // markers may borrow the folder name when the file has no leading title.
            if (isWordMarker)
                return null;
            title = context.FolderTitle;
        }

        if (string.IsNullOrWhiteSpace(value: title) || title.Length <= 1)
            return null;

        int episode = match.Groups[groupname: "num"].Success
            ? int.Parse(s: match.Groups[groupname: "num"].Value)
            : 1;

        return new(filePath: context.Title)
        {
            Title = title,
            Season = 0,
            Episode = episode,
            IsSeries = true,
            IsSuccess = true,
        };
    }
}
