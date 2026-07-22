// Vendored from MovieFileLibrary 3.1.0 (https://github.com/moviecollection/movie-file-library)
// Copyright (c) Peyman Mohammadi — Licensed under the MIT License.
// Imported into this repository to own the parsing logic and named regexes; behavior preserved.

﻿namespace MovieFileLibrary
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The default implementation of <see cref="IMovieDetector"/> interface.
    /// </summary>
    public partial class MovieDetector : IMovieDetector
    {
        /// <inheritdoc/>
        public MovieFile GetInfo(string filePath)
        {
            if (string.IsNullOrWhiteSpace(value: filePath))
            {
                throw new ArgumentException(message: $"'{nameof(filePath)}' cannot be null or whitespace", paramName: nameof(filePath));
            }

            var movieFile = new MovieFile(filePath: filePath);

            string fileName = Path.GetFileName(path: filePath);
            string fileNameWx = Path.GetFileNameWithoutExtension(path: fileName);

            string[] words = GetNormalizedString(str: fileNameWx, separator: ".").Split(separator: '.');

            // Usually the first item is part of the title.
            movieFile.Title = words[0];

            int i;
            for (i = 1; i < words.Length; i++)
            {
                string item = words[i].Trim();

                if (string.IsNullOrWhiteSpace(value: item))
                {
                    continue;
                }

                if (IsYear(item: item))
                {
                    // The Legend of 1900 (1998)
                    // 2001: A Space Odyssey (1968)
                    string? lastYear = words.Skip(count: 1)
                        .Where(predicate: x => IsYear(item: x) && x != item)
                        .LastOrDefault();

                    if (lastYear is null)
                    {
                        movieFile.Year = item;
                    }
                    else
                    {
                        movieFile.Year = lastYear;
                        movieFile.Title += " " + item;
                    }

                    // Scenes.from.a.Marriage.1973.E01.mkv
                    if (!IsSeasonPresent(words: words) && !IsEpisodePresent(words: words))
                    {
                        break;
                    }
                }
                else if (IsSeason(item: item))
                {
                    var sepSeason = item.IndexOf(value: "Se", comparisonType: StringComparison.OrdinalIgnoreCase) >= 0 ? "SE" : "S";
                    var sepEpisode = item.IndexOf(value: "Ep", comparisonType: StringComparison.OrdinalIgnoreCase) >= 0 ? "EP" : "E";

                    // Normal.People.S01E04.1080p.mkv
                    string[] sp = item.Substring(startIndex: sepSeason.Length, length: item.Length - sepSeason.Length).ToUpperInvariant()
                        .Split(separator: new[] { sepEpisode }, options: StringSplitOptions.RemoveEmptyEntries);

                    if (!int.TryParse(s: sp[0], result: out int season))
                    {
                        break;
                    }

                    movieFile.IsSeries = true;
                    movieFile.Season = season;

                    foreach (var episode in sp.Skip(count: 1))
                    {
                        if (int.TryParse(s: episode, result: out int value))
                        {
                            movieFile.AddEpisode(episode: value);
                        }
                    }

                    if (IsEpisodePresent(words: words))
                    {
                        continue;
                    }

                    break;
                }
                else if (IsEpisode(item: item))
                {
                    var separator = item.IndexOf(value: "Ep", comparisonType: StringComparison.OrdinalIgnoreCase) >= 0 ? "EP" : "E";

                    // The.Grand.Tour.S04.E04.1080p.mkv
                    movieFile.IsSeries = true;

                    string e = item.Substring(startIndex: separator.Length, length: item.Length - separator.Length).ToUpperInvariant();

                    if (int.TryParse(s: e, result: out int episode))
                    {
                        movieFile.AddEpisode(episode: episode);
                    }

                    break;
                }
                else if (IsSeasonAndEpisodeWithX(item: item))
                {
                    // Top Gear 17x03 HDTV.mp4
                    string[] split = item.ToUpperInvariant().Split(separator: 'X');

                    if (split.Length == 2 &&
                        int.TryParse(s: split[0], result: out int seasonValue) &&
                        int.TryParse(s: split[1], result: out int episodeValue))
                    {
                        movieFile.IsSeries = true;
                        movieFile.Season = seasonValue;
                        movieFile.AddEpisode(episode: episodeValue);

                        break;
                    }

                    movieFile.Title += " " + item;
                    continue;
                }
                else
                {
                    movieFile.Title += " " + item;
                }
            }

            var remaining = words.Skip(count: i + 1).ToArray();

            if (movieFile.IsSeries && remaining.Any(predicate: x => x.Equals(value: "Special", comparisonType: StringComparison.OrdinalIgnoreCase)))
            {
                movieFile.IsSpecialEpisode = true;
            }

            // Find the imdb id (e.g. Batman Begins (2005) {imdb-tt0372784}.mkv).
            var imdb1 = Array.FindIndex(array: remaining, match: t => t.Equals(value: "imdb", comparisonType: StringComparison.OrdinalIgnoreCase));
            var imdb2 = Array.FindIndex(array: remaining, match: t => t.Equals(value: "imdbid", comparisonType: StringComparison.OrdinalIgnoreCase));

            if (imdb1 >= 0)
            {
                movieFile.ImdbId = remaining.ElementAtOrDefault(index: imdb1 + 1);
            }
            else if (imdb2 >= 0)
            {
                movieFile.ImdbId = remaining.ElementAtOrDefault(index: imdb2 + 1);
            }

            movieFile.IsSuccess = true;
            return movieFile;
        }

        private static string GetNormalizedString(string str, string separator)
        {
            var items = new[] { " ", "(", ")", "_", "-", "..", "–", "[", "]", "{", "}" };

            foreach (string item in items)
            {
                if (str.Contains(value: item))
                {
                    str = str.Replace(oldValue: item, newValue: separator);
                }
            }

            return str;
        }

        private static bool IsSeasonAndEpisodeWithX(string item)
        {
            return SeasonEpisodeXRegex().IsMatch(input: item);
        }

        private static bool IsYear(string item)
        {
            return YearRegex().IsMatch(input: item);
        }

        private static bool IsSeason(string item)
        {
            // S01E01, Se02Ep01
            if (SeasonEpisodeRegex().IsMatch(input: item))
            {
                return true;
            }

            // S01, Se02
            if (SeasonOnlyRegex().IsMatch(input: item))
            {
                return true;
            }

            return false;
        }

        private static bool IsEpisode(string item)
        {
            return EpisodeOnlyRegex().IsMatch(input: item);
        }

        private static bool IsSeasonPresent(string[] words)
        {
            return words.Any(predicate: x => IsSeason(item: x));
        }

        private static bool IsEpisodePresent(string[] words)
        {
            return words.Any(predicate: x => IsEpisode(item: x));
        }
        [GeneratedRegex(pattern: "([0-9]{1,2})([xX])([0-9]{1,2})")]
        private static partial Regex SeasonEpisodeXRegex();

        [GeneratedRegex(pattern: "^(19|20)[0-9][0-9]")]
        private static partial Regex YearRegex();

        [GeneratedRegex(pattern: "^Se?([0-9]{1,2})Ep?([0-9]{1,2})", options: RegexOptions.IgnoreCase)]
        private static partial Regex SeasonEpisodeRegex();

        [GeneratedRegex(pattern: "^Se?([0-9]{1,2})$", options: RegexOptions.IgnoreCase)]
        private static partial Regex SeasonOnlyRegex();

        [GeneratedRegex(pattern: "^Ep?([0-9]{1,2})", options: RegexOptions.IgnoreCase)]
        private static partial Regex EpisodeOnlyRegex();

    }
}
