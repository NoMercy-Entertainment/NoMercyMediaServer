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
using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.Api.DTOs.Media;

public record CollectionResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; } = string.Empty;

    [JsonProperty(propertyName: "duration")]
    public int Duration { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "collection")]
    public CollectionMovieDto[] Collection { get; set; } = [];

    [JsonProperty(propertyName: "number_of_items")]
    public int? NumberOfItems { get; set; }

    [JsonProperty(propertyName: "have_items")]
    public int? HaveItems { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "watched")]
    public bool Watched { get; set; }

    [JsonProperty(propertyName: "genres")]
    public GenreDto[] Genres { get; set; } = [];

    [JsonProperty(propertyName: "total_duration")]
    public int TotalDuration { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "keywords")]
    public IEnumerable<string> Keywords { get; set; } = [];

    [JsonProperty(propertyName: "cast")]
    public PeopleDto[] Cast { get; set; } = [];

    [JsonProperty(propertyName: "crew")]
    public PeopleDto[] Crew { get; set; } = [];

    [JsonProperty(propertyName: "backdrops")]
    public ImageDto[] Backdrops { get; set; } = [];

    [JsonProperty(propertyName: "posters")]
    public ImageDto[] Posters { get; set; } = [];

    [JsonProperty(propertyName: "content_ratings")]
    public ContentRating[] ContentRatings { get; set; } = [];

    [JsonProperty(propertyName: "vote_average")]
    public double VoteAverage { get; set; }

    public CollectionResponseItemDto(Collection? collection)
    {
        if (collection is null)
            return;

        string? title = collection.Translations.FirstOrDefault()?.Title;
        string? overview = collection.Translations.FirstOrDefault()?.Overview;

        Id = collection.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : collection.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : collection.Overview;
        Backdrop = collection.Backdrop;
        Poster = collection.Poster;
        TitleSort = collection.TitleSort;

        Type = MediaTypes.CollectionMediaType;
        MediaType = MediaTypes.CollectionMediaType;
        Link = new(uriString: $"/collection/{Id}", uriKind: UriKind.Relative);

        ColorPalette = collection.ColorPalette;
        NumberOfItems = collection.Parts;
        HaveItems = collection.CollectionMovies.Count(predicate: collectionMovie =>
            collectionMovie.Movie.VideoFiles.Count > 0
        );

        TotalDuration = collection.CollectionMovies.Sum(selector: item => item.Movie.Runtime * 60 ?? 0);

        Favorite = collection.CollectionUser.Count != 0;
        Watched =
            collection.CollectionMovies.Count(predicate: collectionMovie =>
                collectionMovie.Movie.MovieUser.Count != 0
            ) == collection.CollectionMovies.Count;

        Duration = (int)
            collection
                .CollectionMovies.Select(selector: movie =>
                    movie.Movie.VideoFiles.FirstOrDefault()?.Duration?.ToSeconds()
                    ?? movie.Movie.Runtime * 60
                    ?? 0
                )
                .Average();

        VoteAverage =
            collection
                .CollectionMovies.Where(predicate: movie => movie.Movie.VoteAverage != null)
                .Select(selector: movie => movie.Movie.VoteAverage)
                .Average()
            ?? 0;

        Keywords = collection
            .CollectionMovies.SelectMany(selector: movie => movie.Movie.KeywordMovies)
            .DistinctBy(keySelector: keyword => keyword.KeywordId)
            .Select(selector: keywordMovie => keywordMovie.Keyword.Name)
            .OrderBy(keySelector: keyword => keyword)
            .ToArray();

        Genres = collection
            .CollectionMovies.SelectMany(selector: movie => movie.Movie.GenreMovies)
            .DistinctBy(keySelector: genreMovie => genreMovie.GenreId)
            .Select(selector: genreMovie => new GenreDto(genreMovie: genreMovie))
            .ToArray();

        ContentRatings = collection
            .CollectionMovies.SelectMany(selector: collectionMovie =>
                collectionMovie.Movie.CertificationMovies
            )
            .DistinctBy(keySelector: certification => certification.Certification.Iso31661)
            .Select(selector: certificationMovie => new ContentRating
            {
                Rating = certificationMovie.Certification.Rating,
                Iso31661 = certificationMovie.Certification.Iso31661,
            })
            .ToArray();

        Collection = collection
            .CollectionMovies.OrderBy(keySelector: movie => movie.Movie.ReleaseDate)
            .Select(selector: movie => new CollectionMovieDto(movie: movie.Movie))
            .ToArray();

        Backdrops = collection
            .CollectionMovies.SelectMany(selector: movie => movie.Movie.Images)
            .Where(predicate: media => media.Type == "backdrop")
            .Select(selector: media => new ImageDto(media: media))
            .OrderByDescending(keySelector: image => image.VoteAverage)
            .ToArray();

        Posters = collection
            .CollectionMovies.SelectMany(selector: movie => movie.Movie.Images)
            .Where(predicate: media => media.Type == "poster")
            .Select(selector: media => new ImageDto(media: media))
            .OrderByDescending(keySelector: image => image.VoteAverage)
            .ToArray();

        Cast = collection
            .CollectionMovies.SelectMany(selector: movie => movie.Movie.Cast)
            .Select(selector: cast => new PeopleDto(cast: cast))
            .OrderBy(keySelector: cast => cast.Order)
            .DistinctBy(keySelector: people => people.Id)
            .ToArray();

        Crew = collection
            .CollectionMovies.SelectMany(selector: movie => movie.Movie.Crew)
            .Select(selector: crew => new PeopleDto(crew: crew))
            .OrderBy(keySelector: crew => crew.Order)
            .DistinctBy(keySelector: people => people.Id)
            .ToArray();
    }

    public CollectionResponseItemDto(TmdbCollectionAppends tmdbCollectionAppends)
    {
        string? title = tmdbCollectionAppends
            .Translations.Translations.FirstOrDefault()
            ?.Data.Title;
        string? overview = tmdbCollectionAppends
            .Translations.Translations.FirstOrDefault()
            ?.Data.Overview;

        Id = tmdbCollectionAppends.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : tmdbCollectionAppends.Name;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : tmdbCollectionAppends.Overview;
        Backdrop = tmdbCollectionAppends.BackdropPath;
        Poster = tmdbCollectionAppends.PosterPath;
        TitleSort = tmdbCollectionAppends.Name.TitleSort();
        Type = MediaTypes.CollectionMediaType;
        MediaType = MediaTypes.CollectionMediaType;
        ColorPalette = new();
        NumberOfItems = tmdbCollectionAppends.Parts.Length;
        HaveItems = 0;
        Favorite = false;
        Link = new(uriString: $"/collection/{Id}", uriKind: UriKind.Relative);

        VoteAverage =
            tmdbCollectionAppends
                .Parts.Where(predicate: movie => movie.VoteAverage > 0)
                .Average(selector: movie => (double?)movie.VoteAverage)
            ?? 0;

        Keywords = [];

        Genres = [];
        Cast = [];
        Crew = [];

        Collection = tmdbCollectionAppends
            .Parts.OrderBy(keySelector: item => item.TitleSort())
            .Select(selector: movie => new CollectionMovieDto(tmdbMovie: movie))
            .ToArray();

        Backdrops = tmdbCollectionAppends
            .Images.Backdrops.Select(selector: media => new ImageDto(media: media))
            .OrderByDescending(keySelector: image => image.VoteAverage)
            .ToArray();
        Posters = tmdbCollectionAppends
            .Images.Posters.Select(selector: media => new ImageDto(media: media))
            .OrderByDescending(keySelector: image => image.VoteAverage)
            .ToArray();
    }
}
