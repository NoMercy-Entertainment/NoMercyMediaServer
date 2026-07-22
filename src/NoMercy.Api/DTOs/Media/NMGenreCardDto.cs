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
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.DTOs.Media;

public class NmGenreCardDto
{
    [JsonProperty(propertyName: "id")]
    public dynamic? Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

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

    public NmGenreCardDto()
    {
        //
    }

    public NmGenreCardDto(Movie movie, string country)
    {
        string? title = movie.Translations.FirstOrDefault()?.Title;
        string? overview = movie.Translations.FirstOrDefault()?.Overview;

        Id = movie.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : movie.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : movie.Overview;
        Poster = movie.Poster;
        Backdrop = movie.Backdrop;
        Logo = movie.Images.FirstOrDefault(predicate: image => image.Type == "logo")?.FilePath;
        TitleSort = movie.Title.TitleSort(date: movie.ReleaseDate);
        Year = movie.ReleaseDate.ParseYear();

        Type = "genre";
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = 1;
        HaveItems = movie.VideoFiles.Count(predicate: v => v.Folder != null);

        ColorPalette = movie.ColorPalette;

        ContentRatings = movie
            .CertificationMovies.Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationMovie => new ContentRating
            {
                Rating = certificationMovie.Certification.Rating,
                Iso31661 = certificationMovie.Certification.Iso31661,
            });
    }

    public NmGenreCardDto(Tv tv, string country)
    {
        string? title = tv.Translations.FirstOrDefault()?.Title;
        string? overview = tv.Translations.FirstOrDefault()?.Overview;

        Id = tv.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : tv.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : tv.Overview;
        Poster = tv.Poster;
        Backdrop = tv.Backdrop;
        Logo = tv.Images.FirstOrDefault(predicate: image => image.Type == "logo")?.FilePath;
        TitleSort = tv.Title.TitleSort(date: tv.FirstAirDate);
        Year = tv.FirstAirDate.ParseYear();

        Type = "genre";
        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = tv.NumberOfEpisodes;
        HaveItems = tv.Episodes.Count(predicate: episode => episode.VideoFiles.Any(predicate: v => v.Folder != null));

        ColorPalette = tv.ColorPalette;

        ContentRatings = tv
            .CertificationTvs.Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationTv => new ContentRating
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661,
            });
    }

    public NmGenreCardDto(Collection collection, string country)
    {
        string? title = collection.Translations.FirstOrDefault()?.Title;
        string? overview = collection.Translations.FirstOrDefault()?.Overview;

        Id = collection.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : collection.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : collection.Overview;
        Poster = collection.Poster;
        Backdrop = collection.Backdrop;
        Logo = collection.Images.FirstOrDefault(predicate: image => image.Type == "logo")?.FilePath;
        TitleSort = collection.Title.TitleSort(
            date: collection.CollectionMovies.MinBy(keySelector: movie => movie.Movie.ReleaseDate)?.Movie.ReleaseDate
        );
        Year = collection
            .CollectionMovies.MinBy(keySelector: movie => movie.Movie.ReleaseDate)
            ?.Movie.ReleaseDate.ParseYear();

        Type = "genre";
        Link = new(uriString: $"/collection/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = collection.CollectionMovies.Count;
        HaveItems = collection.CollectionMovies.Count(predicate: movie =>
            movie.Movie.VideoFiles.Any(predicate: v => v.Folder != null)
        );

        ColorPalette = collection.ColorPalette;

        ContentRatings = collection
            .CollectionMovies.SelectMany(selector: collectionMovie =>
                collectionMovie.Movie.CertificationMovies
            )
            .Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationMovie => new ContentRating
            {
                Rating = certificationMovie.Certification.Rating,
                Iso31661 = certificationMovie.Certification.Iso31661,
            });
    }

    public NmGenreCardDto(Special special, string country)
    {
        Id = special.Id;
        Title = special.Title;
        Overview = special.Overview;
        Poster = special.Poster;
        Backdrop = special.Backdrop;
        Logo = special.Logo;
        TitleSort = special.Title.TitleSort();
        Year =
            special.Items.MinBy(keySelector: movie => movie.Movie?.ReleaseDate)?.Movie?.ReleaseDate.ParseYear()
            ?? special
                .Items.Select(selector: tv => tv.Episode?.Tv)
                .FirstOrDefault()
                ?.FirstAirDate.ParseYear();

        Type = "genre";
        Link = new(uriString: $"/specials/{Id}", uriKind: UriKind.Relative);

        NumberOfItems = special.Items.Count;

        int haveMovies = special
            .Items.Select(selector: item => item.Movie)
            .Count(predicate: movie => movie is not null && movie.VideoFiles.Count != 0);

        int haveEpisodes = special
            .Items.Select(selector: item => item.Episode)
            .Count(predicate: movie => movie is not null && movie.VideoFiles.Count != 0);

        HaveItems = haveMovies + haveEpisodes;

        ColorPalette = special.ColorPalette;

        ContentRatings = special
            .Items.SelectMany(selector: item =>
                item.Movie?.CertificationMovies ?? Enumerable.Empty<CertificationMovie>()
            )
            .Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationMovie => new ContentRating
            {
                Rating = certificationMovie.Certification.Rating,
                Iso31661 = certificationMovie.Certification.Iso31661,
            });
    }

    public NmGenreCardDto(Genre genre)
    {
        Id = genre.Id;
        Title = genre.Name.ToTitleCase();
        TitleSort = genre.Name;

        Type = "genre";
        Link = new(uriString: $"/genres/{genre.Id}", uriKind: UriKind.Relative);
        NumberOfItems = genre.GenreMovies.Count + genre.GenreTvShows.Count;
        HaveItems =
            genre.GenreMovies.Count(predicate: genreMovie =>
                genreMovie.Movie.VideoFiles.Any(predicate: v => v.Folder != null)
            )
            + genre.GenreTvShows.Count(predicate: genreTv =>
                genreTv.Tv.Episodes.Any(predicate: episode => episode.VideoFiles.Any(predicate: v => v.Folder != null))
            );
    }

    public NmGenreCardDto(MusicGenre genre)
    {
        Id = genre.Id;
        Title = genre.Name.ToTitleCase();
        TitleSort = genre.Name.TitleSort();

        Type = "genre";
        Link = new(uriString: $"/music/genres/{genre.Id}", uriKind: UriKind.Relative);
        NumberOfItems = genre.MusicGenreTracks.Count;
        HaveItems = genre.MusicGenreTracks.Count;
    }

    public NmGenreCardDto(MusicGenreCardDto genre)
    {
        Id = genre.Id;
        Title = genre.Name.ToTitleCase();
        TitleSort = genre.Name.TitleSort();

        Type = "genre";
        Link = new(uriString: $"/music/genres/{genre.Id}", uriKind: UriKind.Relative);
        NumberOfItems = genre.TrackCount;
        HaveItems = genre.TrackCount;
    }
}
