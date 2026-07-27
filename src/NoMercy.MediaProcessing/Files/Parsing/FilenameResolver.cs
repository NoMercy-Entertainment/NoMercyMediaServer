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

namespace NoMercy.MediaProcessing.Files.Parsing;

/// <summary>
/// Everything the file list knows about a name before a provider is asked:
/// which adapter claimed it, the year, and the season/episode the folder or a
/// trailing number fills in.
/// <para>
/// The local and driver-aware list paths ran their own copy of these rules, and
/// a copy is a place for them to drift — the "take the season from a Season N
/// folder" rule reached one of them a release after the other. One resolver, one
/// set of rules, and the corpus tests exercise what both paths actually run.
/// </para>
/// </summary>
public sealed partial class FilenameResolver(IFilenameParserPipeline pipeline)
{
    /// <param name="fileNameWithExtension">Name only, no directory.</param>
    /// <param name="directoryName">The directory the file sits in, for the "Season N folder" and folder-title rules.</param>
    /// <param name="pathForTitle">The full path the adapters use to seed an unmatched title.</param>
    public ResolvedName Resolve(
        string fileNameWithExtension,
        string? directoryName,
        string pathForTitle,
        string libraryType
    )
    {
        string rawFileName = Path.GetFileNameWithoutExtension(fileNameWithExtension);

        // A [tmdb-1234] hint baked into the name, e.g. "[tmdb-553604]Spring (2019).mkv".
        int? overrideTmdbId = rawFileName.TryGetTmdbHint();

        string cleanedForYear = StringExtensions
            .RemoveBracketedString()
            .Replace(rawFileName, string.Empty);
        string? extractedYear = cleanedForYear.TryGetYear();

        string title = StringExtensions
            .RemoveBracketedString()
            .Replace(pathForTitle.Replace("v2", ""), string.Empty);

        string cleanedFileName = VersionSuffix()
            .Replace(
                StringExtensions.RemoveBracketedString().Replace(rawFileName, string.Empty),
                string.Empty
            )
            .Trim();

        MovieFile parsed = pipeline.Parse(
            new()
            {
                FileNameWithExtension = fileNameWithExtension,
                DirectoryName = directoryName,
                Title = title,
                CleanedFileName = cleanedFileName,
                FolderTitle = TitleFromFolder(directoryName),
                LibraryType = libraryType,
            }
        );

        parsed.Year = extractedYear ?? parsed.Year;
        if (parsed.Title is null)
            return new(parsed, overrideTmdbId, SeasonExplicit: false, AirDate: null);

        parsed.Title = StringExtensions
            .RemoveParenthesizedString()
            .Replace(parsed.Title, string.Empty)
            .Trim();

        // Whether the season came from the name or was defaulted, which is what
        // decides if the absolute-index fallback stays available downstream.
        bool seasonExplicit = parsed.Season.HasValue;

        // Dated daily episode (yyyy.mm.dd): the air date is the key, so any stray
        // number that would be misread as an episode goes. The resolver maps the
        // date to the episode that aired that day.
        DateOnly? airDate = libraryType is MediaTypes.TvMediaType or MediaTypes.AnimeMediaType
            ? DailyEpisodeParser.TryGetAirDate(rawFileName)
            : null;
        if (airDate.HasValue)
        {
            parsed.Season = null;
            parsed.Episode = null;
            return new(parsed, overrideTmdbId, seasonExplicit, airDate);
        }

        // A raw disc rip is named for its track ("The Pink Panther Volume 1_t12"),
        // and a track index is not an episode. The only other number in the name
        // is the volume, so every track on a disc was claiming that volume's
        // first episode — 29 files on two slots. Nothing here is knowable, so
        // nothing is guessed and the file goes to the operator to place.
        if (DiscTrackMarker().IsMatch(cleanedFileName))
        {
            parsed.Season = null;
            parsed.Episode = null;
            return new(parsed, overrideTmdbId, seasonExplicit, airDate);
        }

        // Plex-style layout: a file that omits the season but lives in a
        // "Season N" folder takes the season from the folder rather than
        // defaulting to 1. seasonExplicit deliberately keeps the name-derived
        // value so the absolute-index fallback stays available.
        int? folderSeason = directoryName.TryGetFolderSeason();

        if (parsed is { Episode: not null, Season: null })
            parsed.Season = folderSeason ?? 1;

        if (parsed is { Season: null, Episode: null })
        {
            Match numberMatch = MatchNumbers().Match(parsed.Title);
            if (numberMatch.Success)
            {
                parsed.Season = folderSeason ?? 1;
                parsed.Episode = int.Parse(numberMatch.Value);
                parsed.Title = MatchNumbers().Split(parsed.Title).FirstOrDefault()?.Trim();
            }
        }

        return new(parsed, overrideTmdbId, seasonExplicit, airDate);
    }

    internal static string TitleFromFolder(string? directoryName)
    {
        string? folderName = Path.GetFileName(directoryName);
        if (string.IsNullOrWhiteSpace(folderName))
            return "";

        string cleaned = StringExtensions.RemoveBracketedString().Replace(folderName, string.Empty);
        cleaned = StringExtensions.RemoveParenthesizedString().Replace(cleaned, string.Empty);

        Match seasonTag = StringExtensions.MatchSeasonTag().Match(cleaned);
        if (seasonTag is { Success: true, Index: > 0 })
            cleaned = cleaned[..seasonTag.Index];

        string folderTitle = cleaned
            .Replace('.', ' ')
            .Replace('_', ' ')
            .TrimEnd('-', '.', '_', ' ')
            .Trim();

        // The year is captured separately by TryGetYear, so a trailing one here
        // is noise in the title.
        Match yearInFolder = StringExtensions.MatchYearRegex().Match(folderTitle);
        if (yearInFolder.Success)
            folderTitle = folderTitle[..yearInFolder.Index].TrimEnd('-', '.', '_', ' ');

        return folderTitle;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex MatchNumbers();

    /// <summary>
    /// A re-release marks its version on the episode number itself — "Dororo
    /// (2019) - 01v2", "Detective Conan - 678v2". The suffix glues to the digits,
    /// so every matcher that wants a standalone number rejected both halves and
    /// the episode was lost entirely. Anchored to a preceding digit, so a title
    /// that merely contains a v ("NieR-Automata Ver1.1a") is untouched.
    /// </summary>
    [GeneratedRegex(@"(?<=\d)v\d{1,2}(?![A-Za-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex VersionSuffix();

    /// <summary>A MakeMKV-style disc track suffix — "…Volume 1_t12".</summary>
    [GeneratedRegex(@"_t\d{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex DiscTrackMarker();
}

/// <param name="OverrideTmdbId">A [tmdb-1234] hint from the name, which outranks any search.</param>
/// <param name="SeasonExplicit">The season came from the name, not from a folder or a default.</param>
public readonly record struct ResolvedName(
    MovieFile Parsed,
    int? OverrideTmdbId,
    bool SeasonExplicit,
    DateOnly? AirDate
);
