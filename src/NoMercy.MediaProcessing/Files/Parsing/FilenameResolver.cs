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

        // The episode marker is not part of the show's name. Left on, the title
        // that goes to a provider is "Never-Ending Summer - S01E19" — a search
        // string no catalogue holds, answered with whatever scores least badly.
        // The number-only shape ("- 175") was already trimmed; this is the same
        // rule for the shape that spells the season out.
        Match episodeMarker = TitleEpisodeMarker().Match(parsed.Title);
        if (episodeMarker is { Success: true, Index: > 0 })
            parsed.Title = parsed.Title[..episodeMarker.Index].TrimEnd(' ', '-', '.', '_');

        // A file named for its ROLE rather than its show carries no title at
        // all. "Opening 06", "Ending 18", "Menu - 01", "NCED - 01",
        // "OVA-Water Slider" — every one of those is a word describing what the
        // file is inside a release, and the show name is in the folder holding
        // it.
        //
        // Handing that word to a provider as though it were a title is how
        // "Ending 18.mkv" became episode 19 of Never-Ending Summer, "Menu -
        // 01.mkv" became Great British Menu, "Opening 06.mkv" became an episode
        // of Opening Act, and "NCED - 01.mkv" became Flavor, NC. A search will
        // always return something; that is exactly why it must not be asked.
        if (IsRoleWordRatherThanTitle(parsed.Title))
        {
            string folderTitle = TitleFromFolder(directoryName);

            // No title in the name and none in the folder either means nobody
            // knows what this is. Unmatched is a file the operator can place;
            // a confident wrong answer is one they have to find first.
            parsed.Title = folderTitle.Length > 0 ? folderTitle : null;
        }

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

        // A creditless opening or ending — NCOP, NCED, and the textless and
        // "clean" spellings of the same thing — is the sequence with the credits
        // taken off, not an episode. It carries no episode number of its own, so
        // the only numbers in the name belong to the season and the version, and
        // every one of them was read as episode one: a show's NCOP and its NCED
        // both landed on S00E01 and each overwrote the other.
        if (CreditlessSequenceMarker().IsMatch(cleanedFileName))
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

        // A name with no episode marker has no episode. This used to reach into
        // the TITLE for the first number it could find and call that the episode,
        // which is where every mismatch this parser has produced came from: a
        // creditless opening and its matching ending both landing on episode one
        // and overwriting each other, a disc's tracks all claiming their volume's
        // first episode, a show's specials collapsing onto the same slot. The
        // titles alone would have been enough to distrust it — "Mob Psycho 100"
        // is not episode 100 and "86" is not episode 86.
        //
        // A real bare-number episode ("Fairy Tail - 175") never reached here;
        // the absolute-index adapter claims those, and this ran only once every
        // adapter had already declined. So there was nothing left for it to be
        // right about, and a file it cannot place now says so and goes to the
        // operator instead of quietly taking someone else's slot.
        return new(parsed, overrideTmdbId, seasonExplicit, airDate);
    }

    /// <summary>
    /// Words that describe a file's place in a release rather than name a show:
    /// the creditless sequences, the disc furniture, the promos and the extras.
    /// <para>Deliberately matched only when the word is ALL that is left of the
    /// title. "Opening Act" and "Great British Menu" are real shows and stay
    /// real shows; it is the file called nothing but <c>Opening 06</c> that has
    /// no title to give.</para>
    /// </summary>
    private static readonly HashSet<string> RoleWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "op",
        "ed",
        "opening",
        "ending",
        "ncop",
        "nced",
        "nc",
        "creditless",
        "textless",
        "clean",
        "ova",
        "oav",
        "oad",
        "menu",
        "preview",
        "previews",
        "trailer",
        "trailers",
        "promo",
        "promos",
        "pv",
        "cm",
        "sp",
        "special",
        "specials",
        "extra",
        "extras",
        "bonus",
        "credits",
        "intro",
        "outro",
        "teaser",
        "recap",
        "digest",
        "interview",
        "commentary",
        "disc",
        "disk",
        "part",
        "chapter",
        "title",
        "untitled",
        "video",
        "track",
    };

    /// <summary>
    /// Whether <paramref name="title"/> is a role word (or nothing but numbers)
    /// once the numbering and separators a release puts around it are removed.
    /// </summary>
    private static bool IsRoleWordRatherThanTitle(string title)
    {
        string stripped = RoleWordNoise().Replace(title, " ").Trim();

        if (stripped.Length == 0)
            return true;

        string[] words = stripped.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return words.Length > 0 && words.All(RoleWords.Contains);
    }

    /// <summary>
    /// The numbering and punctuation a release wraps a role word in — the
    /// "- 01" of "NCED - 01", the "06" of "Opening 06", the hyphen in
    /// "OVA-Water Slider".
    /// </summary>
    [GeneratedRegex(@"[\d\.\-_\[\]\(\)#]+")]
    private static partial Regex RoleWordNoise();

    /// <summary>
    /// An explicit season/episode marker sitting inside what was taken for the
    /// title. Anything from there on describes the episode, not the show.
    /// </summary>
    [GeneratedRegex(
        @"(?<![A-Za-z0-9])S\d{1,4}[\s._-]*E\d{1,4}(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex TitleEpisodeMarker();

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

    /// <summary>
    /// A creditless opening or ending. NC is the common prefix (NCOP / NCED),
    /// and the same sequences ship as "Textless OP" or "Clean ED" often enough
    /// to be worth matching. The boundaries are what keep this off real titles:
    /// without them "NCOP" matches inside a word and "clean" matches any show
    /// with Clean in its name.
    /// </summary>
    [GeneratedRegex(
        @"(?<![A-Za-z0-9])(?:NC(?:OP|ED)|(?:textless|clean|creditless)[\s\._-]*(?:OP|ED|opening|ending))(?![A-Za-z])",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex CreditlessSequenceMarker();
}

/// <param name="OverrideTmdbId">A [tmdb-1234] hint from the name, which outranks any search.</param>
/// <param name="SeasonExplicit">The season came from the name, not from a folder or a default.</param>
public readonly record struct ResolvedName(
    MovieFile Parsed,
    int? OverrideTmdbId,
    bool SeasonExplicit,
    DateOnly? AirDate
);
