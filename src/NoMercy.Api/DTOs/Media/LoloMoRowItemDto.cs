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
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record LoloMoRowItemDto
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string? MediaType { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "genres")]
    public GenreDto[]? LoloMos { get; set; }

    [JsonProperty(propertyName: "rating")]
    public RatingClass? Rating { get; set; }

    [JsonProperty(propertyName: "videos")]
    public VideoDto[]? Videos { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    public LoloMoRowItemDto(GenreMovie genreMovie)
    {
        Id = genreMovie.Movie.Id;
        Title = genreMovie.Movie.Title;
        Overview = genreMovie.Movie.Overview;
        Poster = genreMovie.Movie.Poster;
        Backdrop = genreMovie.Movie.Backdrop;
        TitleSort = genreMovie.Movie.Title.TitleSort(date: genreMovie.Movie.ReleaseDate);
        Year = genreMovie.Movie.ReleaseDate.ParseYear();
        MediaType = MediaTypes.MovieMediaType;
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        ColorPalette = genreMovie.Movie.ColorPalette;
    }

    public LoloMoRowItemDto(GenreTv genreTv)
    {
        Id = genreTv.Tv.Id;
        Title = genreTv.Tv.Title;
        Overview = genreTv.Tv.Overview;
        Poster = genreTv.Tv.Poster;
        Backdrop = genreTv.Tv.Backdrop;
        TitleSort = genreTv.Tv.Title.TitleSort(date: genreTv.Tv.FirstAirDate);
        Type = genreTv.Tv.Type;
        Year = genreTv.Tv.FirstAirDate.ParseYear();
        MediaType = MediaTypes.TvMediaType;
        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        ColorPalette = genreTv.Tv.ColorPalette;
    }
}
