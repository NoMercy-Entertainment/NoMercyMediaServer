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
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public record LibraryResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public string Id { get; set; }

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

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "name")]
    public string? Name { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "videoId")]
    public string? VideoId { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "genres")]
    public GenreDto[]? Genres { get; set; }

    [JsonProperty(propertyName: "videos")]
    public VideoDto[] Videos { get; set; } = [];

    public LibraryResponseItemDto(LibraryMovie movie)
    {
        Id = movie.Movie.Id.ToString();
        Backdrop = movie.Movie.Backdrop;
        Logo = movie.Movie.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;
        MediaType = MediaTypes.MovieMediaType;
        Year = movie.Movie.ReleaseDate.ParseYear();
        Overview = movie.Movie.Overview;
        ColorPalette = movie.Movie.ColorPalette;
        Poster = movie.Movie.Poster;
        Title = movie.Movie.Title;
        TitleSort = movie.Movie.Title.TitleSort(date: movie.Movie.ReleaseDate);
        Type = MediaTypes.MovieMediaType;
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        Genres = movie.Movie.GenreMovies.Select(selector: genreMovie => new GenreDto(genreMovie: genreMovie)).ToArray();
        VideoId = movie.Movie.Video;
        Videos = movie
            .Movie.Media.Where(predicate: media => media.Site == "YouTube")
            .Select(selector: media => new VideoDto(media: media))
            .ToArray();
    }

    public LibraryResponseItemDto(LibraryTv tv)
    {
        Id = tv.Tv.Id.ToString();
        Backdrop = tv.Tv.Backdrop;
        Logo = tv.Tv.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;
        Year = tv.Tv.FirstAirDate.ParseYear();
        Overview = tv.Tv.Overview;
        ColorPalette = tv.Tv.ColorPalette;
        Poster = tv.Tv.Poster;
        Title = tv.Tv.Title;
        TitleSort = tv.Tv.Title.TitleSort(date: tv.Tv.FirstAirDate);

        Type = MediaTypes.TvMediaType;
        MediaType = MediaTypes.TvMediaType;
        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = tv.Tv.NumberOfEpisodes;
        HaveItems = tv.Tv.Episodes.Count(predicate: episode => episode.VideoFiles.Any(predicate: v => v.Folder != null));

        Genres = tv.Tv.GenreTvs.Select(selector: genreTv => new GenreDto(genreTv: genreTv)).ToArray();
        VideoId = tv.Tv.Trailer;
        Videos = tv
            .Tv.Media.Where(predicate: media => media.Site == "YouTube")
            .Select(selector: media => new VideoDto(media: media))
            .ToArray();
    }

    public LibraryResponseItemDto(Movie movie)
    {
        Id = movie.Id.ToString();
        Backdrop = movie.Backdrop;
        Logo = movie.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;
        MediaType = MediaTypes.MovieMediaType;
        Year = movie.ReleaseDate.ParseYear();
        Overview = movie.Overview;
        ColorPalette = movie.ColorPalette;
        Poster = movie.Poster;
        Title = movie.Title;
        TitleSort = movie.Title.TitleSort(date: movie.ReleaseDate);
        Type = MediaTypes.MovieMediaType;
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        HaveItems = movie.VideoFiles.Count(predicate: v => v.Folder != null);
        NumberOfItems = 1;

        Genres = movie.GenreMovies.Select(selector: genreMovie => new GenreDto(genreMovie: genreMovie)).ToArray();
        VideoId = movie.Video;
        Videos = movie
            .Media.Where(predicate: media => media.Site == "YouTube")
            .Select(selector: media => new VideoDto(media: media))
            .ToArray();
    }

    public LibraryResponseItemDto(Tv tv)
    {
        Id = tv.Id.ToString();
        Backdrop = tv.Backdrop;
        Logo = tv.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;
        Year = tv.FirstAirDate.ParseYear();
        Overview = tv.Overview;
        ColorPalette = tv.ColorPalette;
        Poster = tv.Poster;
        Title = tv.Title;
        TitleSort = tv.Title.TitleSort(date: tv.FirstAirDate);

        Type = MediaTypes.TvMediaType;
        MediaType = MediaTypes.TvMediaType;
        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);

        NumberOfItems = tv.NumberOfEpisodes;
        HaveItems = tv.Episodes.Count(predicate: episode => episode.VideoFiles.Any(predicate: v => v.Folder != null));

        Genres = tv.GenreTvs.Select(selector: genreTv => new GenreDto(genreTv: genreTv)).ToArray();
        VideoId = tv.Trailer;
        Videos = tv
            .Media.Where(predicate: media => media.Site == "YouTube")
            .Select(selector: media => new VideoDto(media: media))
            .ToArray();
    }

    public LibraryResponseItemDto(CollectionMovie movie)
    {
        Id = movie.Movie.Id.ToString();
        Backdrop = movie.Movie.Backdrop;
        Logo = movie.Movie.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;
        MediaType = MediaTypes.MovieMediaType;
        Year = movie.Movie.ReleaseDate.ParseYear();
        Overview = movie.Movie.Overview;
        ColorPalette = movie.Movie.ColorPalette;
        Poster = movie.Movie.Poster;
        Title = movie.Movie.Title;
        TitleSort = movie.Movie.Title.TitleSort(date: movie.Movie.ReleaseDate);
        Type = MediaTypes.MovieMediaType;
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        HaveItems = movie.Movie.VideoFiles.Count(predicate: v => v.Folder != null);
        NumberOfItems = 1;

        Genres = movie.Movie.GenreMovies.Select(selector: genreMovie => new GenreDto(genreMovie: genreMovie)).ToArray();
        VideoId = movie.Movie.Video;
        Videos = movie
            .Movie.Media.Where(predicate: media => media.Site == "YouTube")
            .Select(selector: media => new VideoDto(media: media))
            .ToArray();
    }

    public LibraryResponseItemDto(Collection collection)
    {
        string title = collection.Translations.FirstOrDefault()?.Title ?? collection.Title;

        string overview = (
            collection.Translations.FirstOrDefault()?.Overview ?? collection.Overview
        ).OrEmpty();

        Id = collection.Id.ToString();
        Title = title;
        Overview = overview;
        Backdrop = collection.Backdrop;
        Logo = collection.Images.FirstOrDefault(predicate: media => media.Type == "logo")?.FilePath;

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

        Type = "specials";
        MediaType = "specials";
        Link = new(uriString: $"/collection/{Id}", uriKind: UriKind.Relative);

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

    public LibraryResponseItemDto(Special special)
    {
        string title = special.Title.OrEmpty();

        string overview = special.Overview.OrEmpty();

        Id = special.Id.ToString();
        Name = title;
        Overview = overview;
        Backdrop = special.Backdrop;
        MediaType = "specials";
        Link = new(uriString: $"/specials/{Id}", uriKind: UriKind.Relative);

        ColorPalette = special.ColorPalette;
        Poster = special.Poster;
        TitleSort = special.Title.TitleSort();

        Type = "specials";
    }

    public LibraryResponseItemDto(Person person)
    {
        string name = person.Translations.FirstOrDefault()?.Title ?? person.Name;

        string biography = (
            person.Translations.FirstOrDefault()?.Biography ?? person.Biography
        ).OrEmpty();

        Id = person.Id.ToString();
        Name = name;
        Overview = biography;

        MediaType = "person";
        Type = "person";
        Link = new(uriString: $"/person/{Id}", uriKind: UriKind.Relative);

        TitleSort = person.Name.TitleSort();
        ColorPalette = person.ColorPalette;
        Poster = person.Profile;
    }
}
