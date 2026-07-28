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
/// markers (OVA, ONA, OAD, SP##, NCOP, NCED) and the words
/// "Special(s)"/"Extra(s)"/"Movie(s)". Maps to season 0; the trailing number (if
/// any) becomes the episode, else 1.
/// <para>
/// A series' films are season-zero content, not episodes — "Overlord - Movie 1"
/// used to reach the absolute-number matcher, which read the film index as an
/// absolute episode and landed the two compilation films on top of the season's
/// real first and second episodes.
/// </para>
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
        @"(?<![A-Za-z0-9])(?:(?<word>specials?|extras?|movies?)|(?<anime>ova|ona|oad|ncop|nced|sp))(?:[\s\.\-_]*(?<num>[0-9]{1,3}))?(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex SpecialMarker();

    /// <summary>
    /// The number a release writes as a word rather than beside the marker —
    /// "Movie.Part1", "Movie - Disc 2".
    /// </summary>
    [GeneratedRegex(
        @"^[\s\.\-_]*(?:part|pt|disc|disk|vol(?:ume)?)[\s\.\-_]*(?<num>[0-9]{1,3})(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex NumberedByWord();

    /// <summary>
    /// A marker with no number of its own is the FIRST such special — unless the
    /// release numbered it by word instead: "Fatal Fury - Movie.Part1" and
    /// "...Movie.Part2" both defaulted to one, so the second film was filed on
    /// top of the first.
    /// </summary>
    private static int NumberAfterMarker(string afterMarker)
    {
        Match numbered = NumberedByWord().Match(afterMarker);
        return numbered.Success ? int.Parse(numbered.Groups["num"].Value) : 1;
    }

    public MovieFile? TryParse(ParseContext context)
    {
        if (
            context.LibraryType != MediaTypes.AnimeMediaType
            && context.LibraryType != MediaTypes.TvMediaType
        )
            return null;

        Match match = SpecialMarker().Match(context.CleanedFileName);
        if (!match.Success)
            return null;

        string beforeMarker = context.CleanedFileName[..match.Index];

        // A BD batch puts the season between the show name and the marker
        // ("Overlord - S01 NCOP"), and everything before the marker is the title,
        // so the show reached the providers as "Overlord - S01" and matched
        // nothing.
        Match seasonTag = StringExtensions.MatchSeasonTag().Match(beforeMarker);
        if (seasonTag is { Success: true, Index: > 0 })
            beforeMarker = beforeMarker[..seasonTag.Index];

        string title = beforeMarker
            .Replace('.', ' ')
            .Replace('_', ' ')
            .TrimEnd('-', ' ')
            .Trim()
            .CleanSeriesTitle();

        bool isWordMarker = match.Groups["word"].Success;

        if (string.IsNullOrWhiteSpace(title) || title.Length <= 1)
        {
            // Word markers double as real show titles; only the unambiguous anime
            // markers may borrow the folder name when the file has no leading title.
            if (isWordMarker)
                return null;
            title = context.FolderTitle;
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length <= 1)
            return null;

        int episode = match.Groups["num"].Success
            ? int.Parse(match.Groups["num"].Value)
            : NumberAfterMarker(context.CleanedFileName[(match.Index + match.Length)..]);

        return new(context.Title)
        {
            Title = title,
            Season = 0,
            Episode = episode,
            IsSeries = true,
            IsSuccess = true,
        };
    }
}
