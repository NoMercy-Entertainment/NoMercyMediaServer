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
    [JsonProperty(propertyName: "id")]
    public dynamic? Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "rating")]
    public RatingClass? Rating { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "content_ratings")]
    public IEnumerable<ContentRating> ContentRatings { get; set; } = [];

    [JsonProperty(propertyName: "have_items")]
    public int? HaveItems { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int? NumberOfItems { get; set; }

    public GenreCardData() { }

    public GenreCardData(Genre genre)
    {
        Id = genre.Id;
        Title = genre.Name;
        TitleSort = genre.Name;
        Type = "genre";
        Link = new(uriString: $"/genres/{genre.Id}", uriKind: UriKind.Relative);
        NumberOfItems = genre.GenreMovies.Count + genre.GenreTvShows.Count;
        HaveItems =
            genre.GenreMovies.Count(predicate: gm => gm.Movie.VideoFiles.Any(predicate: v => v.Folder != null))
            + genre.GenreTvShows.Count(predicate: gt =>
                gt.Tv.Episodes.Any(predicate: e => (
                    e.VideoFiles.Any(predicate: v => v.Folder != null)
                    || e.Tv.Episodes.Any(predicate: o =>
                        o.SeasonNumber == e.SeasonNumber
                        && o.VideoFiles.Any(predicate: w =>
                            w is { Folder: not null, LastEpisodeNumber: not null }
                            && o.EpisodeNumber <= e.EpisodeNumber
                            && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)))
                ))
            );
    }

    public GenreCardData(MusicGenre musicGenre)
    {
        Id = musicGenre.Id;
        Title = musicGenre.Name.ToTitleCase();
        TitleSort = musicGenre.Name.TitleSort();
        Type = "genre";
        Link = new(uriString: $"/music/genres/{musicGenre.Id}", uriKind: UriKind.Relative);
        NumberOfItems = musicGenre.AlbumMusicGenres.Count + musicGenre.ArtistMusicGenres.Count;
        HaveItems =
            musicGenre.AlbumMusicGenres.Count(predicate: ga => ga.Album.AlbumTrack.Count != 0)
            + musicGenre.ArtistMusicGenres.Count(predicate: ga => ga.Artist.ArtistTrack.Count != 0);
    }

    public GenreCardData(GenreWithCountsDto dto)
    {
        Id = dto.Id;
        Title = dto.Name.ToTitleCase();
        TitleSort = dto.Name.ToTitleCase();
        Type = "genre";
        Link = new(uriString: $"/genres/{dto.Id}", uriKind: UriKind.Relative);
        NumberOfItems = dto.TotalMovies + dto.TotalTvShows;
        HaveItems = dto.MoviesWithVideo + dto.TvShowsWithVideo;
    }
}
