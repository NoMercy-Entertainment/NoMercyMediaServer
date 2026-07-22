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
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.MusicBrainz.Models;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Common;

public static class FileNameParsers
{
    private static string Pad(int number, int width)
    {
        return number.ToString().PadLeft(totalWidth: width, paddingChar: '0');
    }

    public static string CreateBaseFolder(TmdbTvShow show)
    {
        return "/"
            + string.Concat(args: [show.Name.CleanFileName(), ".(", show.FirstAirDate.ParseYear(), ")"])
                .CleanFileName();
    }

    public static string CreateBaseFolder(Tv show)
    {
        return "/"
            + string.Concat(args: [show.Title.CleanFileName(), ".(", show.FirstAirDate.ParseYear(), ")"])
                .CleanFileName();
    }

    public static string CreateBaseFolder(TmdbMovieDetails tmdbMovie)
    {
        return "/"
            + string.Concat(args: [tmdbMovie.Title, ".(", tmdbMovie.ReleaseDate.ParseYear(), ")"])
                .CleanFileName();
    }

    public static string CreateBaseFolder(Movie movie)
    {
        return "/"
            + string.Concat(args: [movie.Title, ".(", movie.ReleaseDate.ParseYear(), ")"]).CleanFileName();
    }

    public static string CreateEpisodeFolder(TmdbEpisode data, TmdbTvShow show)
    {
        return string.Concat(values: [show.Name, "S", Pad(number: data.SeasonNumber, width: 2), "E", Pad(number: data.EpisodeNumber, width: 2)]
            )
            .CleanFileName();
    }

    public static string CreateTitleSort(string title, DateTime? date = null)
    {
        // Step 1: Capitalize the first letter of the title
        title = char.ToUpper(c: title[index: 0]) + title[1..];

        // Step 2: Remove leading "The", "An", and "A" from the title
        title = Regex.Replace(input: title, pattern: "^The[\\s]*", replacement: "", options: RegexOptions.IgnoreCase);
        title = Regex.Replace(input: title, pattern: "^An[\\s]{1,}", replacement: "", options: RegexOptions.IgnoreCase);
        title = Regex.Replace(input: title, pattern: "^A[\\s]{1,}", replacement: "", options: RegexOptions.IgnoreCase);

        // Step 3: Replace ": " and " and the" with the parsed year (if available) or "."
        string replacement = date != null ? $".{date.ParseYear()}." : ".";
        title = Regex.Replace(input: title, pattern: @":\s|\sand\sthe", replacement: replacement, options: RegexOptions.IgnoreCase);

        // Step 4: Replace all "." with " "
        title = title.Replace(oldValue: ".", newValue: " ");

        // Step 5: Convert the title to lowercase
        title = title.ToLower();

        return title.CleanFileName();
    }

    public static string CreateMediaFolder(Library library, TmdbMovieDetails tmdbMovie)
    {
        string? baseFolder = library.FolderLibraries.FirstOrDefault()?.Folder.Path;
        if (baseFolder is null)
            throw new InvalidOperationException(
                message: $"Library '{library.Title}' has no folders assigned — cannot determine the destination folder for movie '{tmdbMovie.Title}'."
            );

        return string.Concat(str0: baseFolder, str1: "/", str2: CreateBaseFolder(tmdbMovie: tmdbMovie)).CleanFileName();
    }

    public static string CreateMediaFolder(Library library, TmdbTvShow tmdbTv)
    {
        string? baseFolder = library.FolderLibraries.FirstOrDefault()?.Folder.Path;
        if (baseFolder is null)
            throw new InvalidOperationException(
                message: $"Library '{library.Title}' has no folders assigned — cannot determine the destination folder for show '{tmdbTv.Name}'."
            );

        return string.Concat(str0: baseFolder, str1: "/", str2: CreateBaseFolder(show: tmdbTv)).CleanFileName();
    }

    public static string CreateFileName(TmdbMovieDetails tmdbMovie)
    {
        return string.Concat(args: [tmdbMovie.Title, ".(", tmdbMovie.ReleaseDate.ParseYear(), ").NoMercy"])
            .CleanFileName();
    }

    public static string CreateFileName(TmdbEpisode tmdbEpisode, TmdbTvShow tmdbTvShow)
    {
        return string.Concat(values: [tmdbTvShow.Name, ".", Pad(number: tmdbEpisode.SeasonNumber, width: 2), "E", Pad(number: tmdbEpisode.EpisodeNumber, width: 2), ".", tmdbEpisode.Name, ".NoMercy"]
            )
            .CleanFileName();
    }

    public static string? CreateRootFolderName(string folder)
    {
        using MediaContext context = new();
        return context
            .Libraries.Include(navigationPropertyPath: l => l.FolderLibraries)
                .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .SelectMany(selector: l => l.FolderLibraries)
            .FirstOrDefault(predicate: m => folder.Contains(m.Folder.Path))
            ?.Folder.Path;
    }

    public static string CreateBaseFolder(MusicBrainzRecordingAppends music)
    {
        return string.Concat(arg0: music.ArtistCredit[0].Name[index: 0], arg1: "/", arg2: music.ArtistCredit[0].Name)
            .CleanFileName();
    }
}
