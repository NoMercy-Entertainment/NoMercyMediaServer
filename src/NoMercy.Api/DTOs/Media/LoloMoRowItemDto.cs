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
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.DTOs.Media;

public record LoloMoRowItemDto
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty("logo")]
    public string? Logo { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }

    [JsonProperty("overview")]
    public string? Overview { get; set; }

    [JsonProperty("poster")]
    public string? Poster { get; set; }

    [JsonProperty("titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("year")]
    public int? Year { get; set; }

    [JsonProperty("media_type")]
    public string? MediaType { get; set; }

    [JsonProperty("color_palette")]
    public IColorPalettes? ColorPalette { get; set; }

    [JsonProperty("genres")]
    public GenreDto[]? LoloMos { get; set; }

    [JsonProperty("rating")]
    public RatingClass? Rating { get; set; }

    [JsonProperty("videos")]
    public VideoDto[]? Videos { get; set; }

    [JsonProperty("link")]
    public Uri Link { get; set; }

    public LoloMoRowItemDto(GenreMovie genreMovie)
    {
        Id = genreMovie.Movie.Id;
        Title = genreMovie.Movie.Title;
        Overview = genreMovie.Movie.Overview;
        Poster = genreMovie.Movie.Poster;
        Backdrop = genreMovie.Movie.Backdrop;
        TitleSort = genreMovie.Movie.Title.TitleSort(genreMovie.Movie.ReleaseDate);
        Year = genreMovie.Movie.ReleaseDate.ParseYear();
        MediaType = MediaTypes.MovieMediaType;
        Link = new($"/movie/{Id}", UriKind.Relative);
        ColorPalette = genreMovie.Movie.ColorPalette;
    }

    public LoloMoRowItemDto(GenreTv genreTv)
    {
        Id = genreTv.Tv.Id;
        Title = genreTv.Tv.Title;
        Overview = genreTv.Tv.Overview;
        Poster = genreTv.Tv.Poster;
        Backdrop = genreTv.Tv.Backdrop;
        TitleSort = genreTv.Tv.Title.TitleSort(genreTv.Tv.FirstAirDate);
        Type = genreTv.Tv.Type;
        Year = genreTv.Tv.FirstAirDate.ParseYear();
        MediaType = MediaTypes.TvMediaType;
        Link = new($"/tv/{Id}", UriKind.Relative);
        ColorPalette = genreTv.Tv.ColorPalette;
    }
}
