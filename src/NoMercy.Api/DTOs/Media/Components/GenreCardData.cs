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

using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media.Components;

/// <summary>
/// Data for NMGenreCard component - genre category card.
/// </summary>
public record GenreCardData
{
    [JsonProperty("id")]
    public dynamic? Id { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; } = string.Empty;

    [JsonProperty("titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty("rating")]
    public RatingClass? Rating { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty("poster")]
    public string? Poster { get; set; }

    [JsonProperty("logo")]
    public string? Logo { get; set; }

    [JsonProperty("color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty("content_ratings")]
    public IEnumerable<ContentRating> ContentRatings { get; set; } = [];

    [JsonProperty("have_items")]
    public int? HaveItems { get; set; }

    [JsonProperty("number_of_items")]
    public int? NumberOfItems { get; set; }

    public GenreCardData() { }

    public GenreCardData(Genre genre)
    {
        Id = genre.Id;
        Title = genre.Name;
        TitleSort = genre.Name;
        Type = "genre";
        Link = new($"/genres/{genre.Id}", UriKind.Relative);
        NumberOfItems = genre.GenreMovies.Count + genre.GenreTvShows.Count;
        HaveItems =
            genre.GenreMovies.Count(gm => gm.Movie.VideoFiles.Any(v => v.Folder != null))
            + genre.GenreTvShows.Count(gt =>
                gt.Tv.Episodes.Any(e =>
                    (
                        e.VideoFiles.Any(v => v.Folder != null)
                        || e.Tv.Episodes.Any(o =>
                            o.SeasonNumber == e.SeasonNumber
                            && o.VideoFiles.Any(w =>
                                w is { Folder: not null, LastEpisodeNumber: not null }
                                && o.EpisodeNumber <= e.EpisodeNumber
                                && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                            )
                        )
                    )
                )
            );
    }

    public GenreCardData(MusicGenre musicGenre)
    {
        Id = musicGenre.Id;
        Title = musicGenre.Name.ToTitleCase();
        TitleSort = musicGenre.Name.TitleSort();
        Type = "genre";
        Link = new($"/music/genres/{musicGenre.Id}", UriKind.Relative);
        NumberOfItems = musicGenre.AlbumMusicGenres.Count + musicGenre.ArtistMusicGenres.Count;
        HaveItems =
            musicGenre.AlbumMusicGenres.Count(ga => ga.Album.AlbumTrack.Count != 0)
            + musicGenre.ArtistMusicGenres.Count(ga => ga.Artist.ArtistTrack.Count != 0);
    }

    public GenreCardData(GenreWithCountsDto dto)
    {
        Id = dto.Id;
        Title = dto.Name.ToTitleCase();
        TitleSort = dto.Name.ToTitleCase();
        Type = "genre";
        Link = new($"/genres/{dto.Id}", UriKind.Relative);
        NumberOfItems = dto.TotalMovies + dto.TotalTvShows;
        HaveItems = dto.MoviesWithVideo + dto.TvShowsWithVideo;
    }

    public GenreCardData(AnimeThemeWithCountsDto dto)
    {
        Id = dto.Id;
        Title = dto.Name.ToTitleCase();
        TitleSort = dto.Name.ToTitleCase();
        Type = "anime-theme";
        Link = new($"/anime/themes/{dto.Id}", UriKind.Relative);
        NumberOfItems = dto.TotalMovies + dto.TotalTvShows;
        HaveItems = dto.MoviesWithVideo + dto.TvShowsWithVideo;
    }

    public GenreCardData(AnimeDemographicWithCountsDto dto)
    {
        Id = dto.Id;
        Title = dto.Name.ToTitleCase();
        TitleSort = dto.Name.ToTitleCase();
        Type = "anime-demographic";
        Link = new($"/anime/demographics/{dto.Id}", UriKind.Relative);
        NumberOfItems = dto.TotalMovies + dto.TotalTvShows;
        HaveItems = dto.MoviesWithVideo + dto.TvShowsWithVideo;
    }

    public GenreCardData(AnimeSeasonWithCountsDto dto)
    {
        // AnimeSeason has no Translations, so the title is formatted server-side
        // from Year+Quarter (e.g. "Summer 2020") instead of pulled from a
        // Translation row. Locale formatting of the quarter word happens
        // client-side per the spec.
        string title = $"{dto.Quarter.ToTitleCase()} {dto.Year}";
        Id = dto.Id;
        Title = title;
        TitleSort = title;
        Year = dto.Year;
        Type = "anime-season";
        Link = new($"/anime/seasons/{dto.Id}", UriKind.Relative);
        NumberOfItems = dto.TotalMovies + dto.TotalTvShows;
        HaveItems = dto.MoviesWithVideo + dto.TvShowsWithVideo;
    }
}
