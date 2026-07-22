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
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record CollectionsResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "watched")]
    public bool Watched { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int? NumberOfItems { get; set; }

    [JsonProperty(propertyName: "have_items")]
    public int? HaveItems { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "genres")]
    public GenreDto[]? Genres { get; set; }

    [JsonProperty(propertyName: "videoId")]
    public string? VideoId { get; set; }

    [JsonProperty(propertyName: "videos")]
    public VideoDto[]? Videos { get; set; } = [];

    public CollectionsResponseItemDto(CollectionMovie collectionMovie)
    {
        string? title = collectionMovie.Movie.Translations.FirstOrDefault()?.Title;
        string? overview = collectionMovie.Movie.Translations.FirstOrDefault()?.Overview;

        Id = collectionMovie.Movie.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : collectionMovie.Movie.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : collectionMovie.Movie.Overview;

        Backdrop = collectionMovie.Movie.Backdrop;
        Logo = collectionMovie.Movie.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;
        MediaType = "collectionMovie";
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        Year = collectionMovie.Movie.ReleaseDate.ParseYear();
        ColorPalette = collectionMovie.Movie.ColorPalette;
        Poster = collectionMovie.Movie.Poster;
        TitleSort = collectionMovie.Movie.Title.TitleSort(date: collectionMovie.Movie.ReleaseDate);
        Type = "collectionMovie";
        Genres = collectionMovie
            .Movie.GenreMovies.Select(selector: genreMovie => new GenreDto(genreMovie: genreMovie))
            .ToArray();
        VideoId = collectionMovie.Movie.Video;
        Videos = collectionMovie
            .Movie.Media.Where(predicate: media => media.Site == "YouTube")
            .Select(selector: media => new VideoDto(media: media))
            .ToArray();
    }

    public CollectionsResponseItemDto(Collection collection)
    {
        string? title = collection.Translations.FirstOrDefault()?.Title;
        string? overview = collection.Translations.FirstOrDefault()?.Overview;

        Id = collection.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : collection.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : collection.Overview;
        Backdrop = collection.Backdrop;
        Logo = collection.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;
        Link = new(uriString: $"/collection/{Id}", uriKind: UriKind.Relative);
        Year = collection
            .CollectionMovies.MinBy(keySelector: collectionMovie => collectionMovie.Movie.ReleaseDate)
            ?.Movie.ReleaseDate.ParseYear();

        ColorPalette = collection.ColorPalette;
        Poster = collection.Poster;
        TitleSort = collection.Title.TitleSort(
            parseYear: collection
                .CollectionMovies.MinBy(keySelector: collectionMovie => collectionMovie.Movie.ReleaseDate)
                ?.Movie.ReleaseDate.ParseYear()
        );

        MediaType = MediaTypes.CollectionMediaType;
        Type = MediaTypes.CollectionMediaType;

        NumberOfItems = collection.Parts;
        HaveItems = collection.CollectionMovies.Count(predicate: collectionMovie =>
            collectionMovie.Movie.VideoFiles.Any(predicate: v => v.Folder != null)
        );

        Genres = collection
            .CollectionMovies.Select(selector: genreTv => genreTv.Movie)
            .SelectMany(selector: movie => movie.GenreMovies.Select(selector: genreMovie => genreMovie.Genre))
            .Select(selector: genre => new GenreDto(genreMovie: genre))
            .ToArray();

        VideoId = collection.CollectionMovies.FirstOrDefault()?.Movie.Video;
    }
}
