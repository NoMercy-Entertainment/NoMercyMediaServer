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
using NoMercy.Data.DTOs.Specials;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.Api.DTOs.Media;

public class NmCardDto
{
    [JsonProperty(propertyName: "id")]
    public dynamic? Id { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;

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

    [JsonProperty(propertyName: "duration")]
    public int? Duration { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "have_items")]
    public int? HaveItems { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int? NumberOfItems { get; set; }

    public NmCardDto()
    {
        //
    }

    public NmCardDto(Movie movie, string country)
    {
        string? title = movie.Translations.FirstOrDefault()?.Title;
        string? overview = movie.Translations.FirstOrDefault()?.Overview;

        Id = movie.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : movie.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : movie.Overview;
        Poster = movie.Poster;
        Backdrop = movie.Backdrop;
        Logo = movie.Images.FirstOrDefault(predicate: i => i.Type == "logo")?.FilePath;
        TitleSort = movie.Title.TitleSort(date: movie.ReleaseDate);
        Year = movie.ReleaseDate.ParseYear();
        Type = MediaTypes.MovieMediaType;

        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = 1;
        HaveItems = movie.VideoFiles.Count(predicate: v => v.Folder != null);

        ColorPalette = movie.ColorPalette;
        CreatedAt = movie.CreatedAt;

        Rating = movie
            .CertificationMovies.Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationTv => new RatingClass
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661,
                Image =
                    $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg",
            })
            .FirstOrDefault();
    }

    public NmCardDto(Tv tv, string country)
    {
        string? title = tv.Translations.FirstOrDefault()?.Title;
        string? overview = tv.Translations.FirstOrDefault()?.Overview;

        Id = tv.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : tv.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : tv.Overview;
        Poster = tv.Poster;
        Backdrop = tv.Backdrop;
        Logo = tv.Images.FirstOrDefault(predicate: i => i.Type == "logo")?.FilePath;
        TitleSort = tv.Title.TitleSort(date: tv.FirstAirDate);
        Year = tv.FirstAirDate.ParseYear();
        Type = MediaTypes.TvMediaType;
        CreatedAt = tv.CreatedAt;

        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = tv.NumberOfEpisodes;
        HaveItems = tv.Episodes.Count(predicate: episode => episode.VideoFiles.Any(predicate: v => v.Folder != null));

        ColorPalette = tv.ColorPalette;

        Rating = tv
            .CertificationTvs.Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationTv => new RatingClass
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661,
                Image =
                    $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg",
            })
            .FirstOrDefault();
    }

    public NmCardDto(Collection collection, string country)
    {
        string? title = collection.Translations.FirstOrDefault()?.Title;
        string? overview = collection.Translations.FirstOrDefault()?.Overview;

        Id = collection.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : collection.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : collection.Overview;
        Poster = collection.Poster;
        Backdrop = collection.Backdrop;
        Logo = collection.Images.FirstOrDefault(predicate: i => i.Type == "logo")?.FilePath;
        TitleSort = collection.Title.TitleSort(
            date: collection.CollectionMovies.MinBy(keySelector: movie => movie.Movie.ReleaseDate)?.Movie.ReleaseDate
        );
        Year = collection
            .CollectionMovies.MinBy(keySelector: movie => movie.Movie.ReleaseDate)
            ?.Movie.ReleaseDate.ParseYear();
        Type = MediaTypes.CollectionMediaType;

        Link = new(uriString: $"/collection/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = collection.CollectionMovies.Count;
        HaveItems = collection.CollectionMovies.Count(predicate: movie =>
            movie.Movie.VideoFiles.Any(predicate: v => v.Folder != null)
        );

        ColorPalette = collection.ColorPalette;
        CreatedAt = collection.CreatedAt;

        Rating = collection
            .CollectionMovies.SelectMany(selector: collectionMovie =>
                collectionMovie.Movie.CertificationMovies
            )
            .Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationTv => new RatingClass
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661,
                Image =
                    $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg",
            })
            .FirstOrDefault();
    }

    public NmCardDto(Special special, string country)
    {
        Id = special.Id;
        Title = special.Title.OrEmpty();
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
        Type = MediaTypes.SpecialMediaType;

        Link = new(uriString: $"/specials/{Id}", uriKind: UriKind.Relative);

        NumberOfItems = special.Items.Count;
        CreatedAt = special.CreatedAt;

        int haveMovies = special
            .Items.Select(selector: item => item.Movie)
            .Count(predicate: movie => movie is not null && movie.VideoFiles.Count != 0);

        int haveEpisodes = special
            .Items.Select(selector: item => item.Episode)
            .Count(predicate: movie => movie is not null && movie.VideoFiles.Count != 0);

        HaveItems = haveMovies + haveEpisodes;

        ColorPalette = special.ColorPalette;

        Rating = special
            .Items.SelectMany(selector: item =>
                item.Movie?.CertificationMovies ?? Enumerable.Empty<CertificationMovie>()
            )
            .Where(predicate: certificationMovie =>
                certificationMovie.Certification.Iso31661 == "US"
                || certificationMovie.Certification.Iso31661 == country
            )
            .Select(selector: certificationTv => new RatingClass
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661,
                Image =
                    $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg",
            })
            .FirstOrDefault();
    }

    public NmCardDto(HomeMovieCardDto movie, string country)
    {
        Id = movie.Id;
        Title = !string.IsNullOrEmpty(value: movie.TranslatedTitle) ? movie.TranslatedTitle : movie.Title;
        Overview = !string.IsNullOrEmpty(value: movie.TranslatedOverview)
            ? movie.TranslatedOverview
            : movie.Overview;
        Poster = movie.Poster;
        Backdrop = movie.Backdrop;
        Logo = movie.Logo;
        TitleSort = movie.TitleSort;
        Year = movie.ReleaseDate.ParseYear();
        Type = MediaTypes.MovieMediaType;
        CreatedAt = movie.CreatedAt;
        Link = new(uriString: $"/movie/{movie.Id}", uriKind: UriKind.Relative);
        NumberOfItems = 1;
        HaveItems = movie.VideoFileCount;

        ColorPalette = ColorPalette.FromJsonOrNull(json: movie.ColorPalette);

        if (movie.CertificationRating != null)
        {
            Rating = new()
            {
                Rating = movie.CertificationRating,
                Iso31661 = movie.CertificationCountry!,
                Image =
                    $"/{movie.CertificationCountry}/{movie.CertificationCountry}_{movie.CertificationRating}.svg",
            };
        }
    }

    public NmCardDto(HomeTvCardDto tv, string country)
    {
        Id = tv.Id;
        Title = !string.IsNullOrEmpty(value: tv.TranslatedTitle) ? tv.TranslatedTitle : tv.Title;
        Overview = !string.IsNullOrEmpty(value: tv.TranslatedOverview)
            ? tv.TranslatedOverview
            : tv.Overview;
        Poster = tv.Poster;
        Backdrop = tv.Backdrop;
        Logo = tv.Logo;
        TitleSort = tv.TitleSort;
        Year = tv.FirstAirDate.ParseYear();
        Type = MediaTypes.TvMediaType;
        CreatedAt = tv.CreatedAt;
        Link = new(uriString: $"/tv/{tv.Id}", uriKind: UriKind.Relative);
        NumberOfItems = tv.NumberOfEpisodes;
        HaveItems = tv.EpisodesWithVideo;

        ColorPalette = ColorPalette.FromJsonOrNull(json: tv.ColorPalette);

        if (tv.CertificationRating != null)
        {
            Rating = new()
            {
                Rating = tv.CertificationRating,
                Iso31661 = tv.CertificationCountry!,
                Image =
                    $"/{tv.CertificationCountry}/{tv.CertificationCountry}_{tv.CertificationRating}.svg",
            };
        }
    }

    public NmCardDto(CollectionListDto dto, string country)
    {
        Id = dto.Id;
        Title = !string.IsNullOrEmpty(value: dto.TranslatedTitle) ? dto.TranslatedTitle : dto.Title;
        Overview = !string.IsNullOrEmpty(value: dto.TranslatedOverview)
            ? dto.TranslatedOverview
            : dto.Overview;
        Poster = dto.Poster;
        Backdrop = dto.Backdrop;
        Logo = dto.Logo;
        TitleSort = dto.TitleSort;
        Year = dto.FirstMovieYear;
        Type = MediaTypes.CollectionMediaType;
        Link = new(uriString: $"/collection/{dto.Id}", uriKind: UriKind.Relative);
        NumberOfItems = dto.TotalMovies;
        HaveItems = dto.MoviesWithVideo;
        ColorPalette = dto.ColorPalette;
        CreatedAt = dto.CreatedAt;

        if (
            !string.IsNullOrEmpty(value: dto.CertificationRating)
            && !string.IsNullOrEmpty(value: dto.CertificationCountry)
        )
        {
            Rating = new()
            {
                Rating = dto.CertificationRating,
                Iso31661 = dto.CertificationCountry,
                Image =
                    $"/{dto.CertificationCountry}/{dto.CertificationCountry}_{dto.CertificationRating}.svg",
            };
        }
    }

    public NmCardDto(SpecialCardDto dto, string country)
    {
        Id = dto.Id;
        Title = dto.Title;
        Overview = dto.Overview;
        Poster = dto.Poster;
        Backdrop = dto.Backdrop;
        Logo = dto.Logo;
        TitleSort = dto.TitleSort;
        Type = MediaTypes.SpecialMediaType;
        Link = new(uriString: $"/specials/{dto.Id}", uriKind: UriKind.Relative);
        NumberOfItems = dto.NumberOfItems;
        CreatedAt = dto.CreatedAt;
        HaveItems = dto.HaveMovies + dto.HaveEpisodes;

        ColorPalette = ColorPalette.FromJsonOrNull(json: dto.ColorPalette);

        if (dto.CertificationRating != null)
        {
            Rating = new()
            {
                Rating = dto.CertificationRating,
                Iso31661 = dto.CertificationCountry!,
                Image =
                    $"/{dto.CertificationCountry}/{dto.CertificationCountry}_{dto.CertificationRating}.svg",
            };
        }
    }

    public NmCardDto(UserData item, string country)
    {
        Id = (
            item.SpecialId?.ToString()
            ?? item.CollectionId?.ToString()
            ?? item.MovieId?.ToString()
            ?? item.TvId?.ToString()
        ).OrEmpty();

        if (item.Special is not null)
        {
            ColorPalette = item.Special.ColorPalette;
            Poster = item.Special.Poster;
            Backdrop = item.Special.Backdrop;
            Title = item.Special.Title.OrEmpty();
            TitleSort = item.Special.Title.TitleSort();
            Overview = item.Special.Overview;
            Logo = item.Special.Logo;
            Duration = item.VideoFile.Duration?.ToSeconds();
            Type = MediaTypes.SpecialMediaType;

            Link = new(uriString: $"/specials/{Id}/watch", uriKind: UriKind.Relative);

            NumberOfItems = item.Special.Items.Count;
            CreatedAt = item.Special.CreatedAt;

            int availableMovies = item.Special.Items.Count(predicate: specialItem =>
                specialItem is { MovieId: not null, Movie.VideoFiles.Count: > 0 }
            );
            int availableEpisodes = item.Special.Items.Count(predicate: specialItem =>
                specialItem.Episode is { VideoFiles.Count: > 0 }
            );
            HaveItems = availableMovies + availableEpisodes;

            Rating = item
                .Special.Items.SelectMany(selector: specialItem =>
                    specialItem
                        .Episode?.Tv.CertificationTvs.Where(predicate: certificationTv =>
                            certificationTv.Certification.Iso31661 == "US"
                            || certificationTv.Certification.Iso31661 == country
                        )
                        .Select(selector: certificationTv => new RatingClass
                        {
                            Rating = certificationTv.Certification.Rating,
                            Iso31661 = certificationTv.Certification.Iso31661,
                            Image =
                                $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg",
                        })
                    ?? []
                )
                .Concat(
                    second: item.Special.Items.Where(predicate: specialItem => specialItem.MovieId != null)
                        .SelectMany(selector: specialItem =>
                            specialItem
                                .Movie?.CertificationMovies.Where(predicate: certificationMovie =>
                                    certificationMovie.Certification.Iso31661 == "US"
                                    || certificationMovie.Certification.Iso31661 == country
                                )
                                .Select(selector: certificationTv => new RatingClass
                                {
                                    Rating = certificationTv.Certification.Rating,
                                    Iso31661 = certificationTv.Certification.Iso31661,
                                    Image =
                                        $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg",
                                })
                            ?? []
                        )
                )
                .OrderByDescending(keySelector: cert => cert.Order)
                .FirstOrDefault();
        }
        else if (item.Collection is not null)
        {
            ColorPalette = item.Collection.ColorPalette;
            Poster = item.Collection.Poster;
            Backdrop = item.Collection.Backdrop;
            Title = item.Collection.Title;
            TitleSort = item.Collection.Title.TitleSort();
            Overview = item.Collection.Overview;
            Logo = item.Collection.Images.FirstOrDefault(predicate: i => i.Type == "logo")?.FilePath;
            Duration = item.VideoFile.Duration?.ToSeconds();
            Year =
                item.Collection.CollectionMovies.MinBy(keySelector: movie =>
                        movie.Movie.ReleaseDate?.ParseYear()
                    )
                    ?.Movie.ReleaseDate.ParseYear()
                ?? 0;
            Type = MediaTypes.CollectionMediaType;

            Link = new(uriString: $"/collection/{Id}/watch", uriKind: UriKind.Relative);
            CreatedAt = item.Collection.CreatedAt;

            NumberOfItems = item.Collection.CollectionMovies.Count;
            HaveItems = item
                .Collection.CollectionMovies.SelectMany(selector: collectionMovie =>
                    collectionMovie.Movie.VideoFiles
                )
                .Count(predicate: videoFile => videoFile.Folder != null);

            Rating = item
                .Collection.CollectionMovies.SelectMany(selector: collectionMovie =>
                    collectionMovie.Movie.CertificationMovies
                )
                .Where(predicate: certificationMovie =>
                    certificationMovie.Certification.Iso31661 == "US"
                    || certificationMovie.Certification.Iso31661 == country
                )
                .Select(selector: certificationTv => new RatingClass
                {
                    Rating = certificationTv.Certification.Rating,
                    Iso31661 = certificationTv.Certification.Iso31661,
                    Image =
                        $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg",
                })
                .FirstOrDefault();
        }
        else if (item.Movie is not null)
        {
            ColorPalette = item.Movie.ColorPalette;
            Year = item.Movie.ReleaseDate.ParseYear();
            Poster = item.Movie.Poster;
            Backdrop = item.Movie.Backdrop;
            Title = item.Movie.Title;
            TitleSort = item.Movie.Title.TitleSort(date: item.Movie.ReleaseDate);
            Overview = item.Movie.Overview;
            Logo = item.Movie.Images.FirstOrDefault(predicate: i => i.Type == "logo")?.FilePath;
            Duration = item.VideoFile.Duration?.ToSeconds();
            Link = new(uriString: $"/movie/{Id}/watch", uriKind: UriKind.Relative);
            Type = MediaTypes.MovieMediaType;
            CreatedAt = item.Movie.CreatedAt;

            NumberOfItems = 1;
            HaveItems = item.Movie.VideoFiles.Count(predicate: v => v.Folder != null);

            Rating = item
                .Movie.CertificationMovies.Where(predicate: certificationMovie =>
                    certificationMovie.Certification.Iso31661 == "US"
                    || certificationMovie.Certification.Iso31661 == country
                )
                .Select(selector: certificationTv => new RatingClass
                {
                    Rating = certificationTv.Certification.Rating,
                    Iso31661 = certificationTv.Certification.Iso31661,
                    Image =
                        $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg",
                })
                .FirstOrDefault();
        }
        else if (item.Tv is not null)
        {
            ColorPalette = item.Tv.ColorPalette;
            Year = item.Tv.FirstAirDate.ParseYear();
            Poster = item.Tv.Poster;
            Backdrop = item.Tv.Backdrop;
            Title = item.Tv.Title;
            TitleSort = item.Tv.Title.TitleSort(date: item.Tv.FirstAirDate);
            HaveItems = item.Tv.HaveEpisodes;
            Overview = item.Tv.Overview;
            Logo = item.Tv.Images.FirstOrDefault(predicate: i => i.Type == "logo")?.FilePath;
            Duration = item.VideoFile.Duration?.ToSeconds();
            Link = new(uriString: $"/tv/{Id}/watch", uriKind: UriKind.Relative);
            Type = MediaTypes.TvMediaType;
            CreatedAt = item.Tv.CreatedAt;

            NumberOfItems = item.Tv.NumberOfEpisodes;
            HaveItems = item.Tv.Episodes.Count(predicate: episode =>
                episode.VideoFiles.Any(predicate: v => v.Folder != null)
            );

            Rating = item
                .Tv.CertificationTvs.Where(predicate: certificationMovie =>
                    certificationMovie.Certification.Iso31661 == "US"
                    || certificationMovie.Certification.Iso31661 == country
                )
                .Select(selector: certificationTv => new RatingClass
                {
                    Rating = certificationTv.Certification.Rating,
                    Iso31661 = certificationTv.Certification.Iso31661,
                    Image =
                        $"/{certificationTv.Certification.Iso31661}/{certificationTv.Certification.Iso31661}_{certificationTv.Certification.Rating}.svg",
                })
                .FirstOrDefault();
        }
    }

    public NmCardDto(TmdbMovie tmdbMovie)
    {
        Id = tmdbMovie.Id;
        Title = tmdbMovie.Title;
        Overview = tmdbMovie.Overview;
        Id = tmdbMovie.Id;
        Title = tmdbMovie.Title;
        Overview = tmdbMovie.Overview;
        Backdrop = tmdbMovie.BackdropPath;
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        Type = MediaTypes.MovieMediaType;
        ColorPalette = new();
        Poster = tmdbMovie.PosterPath;
        Year = tmdbMovie.ReleaseDate.ParseYear();
        NumberOfItems = 1;
        HaveItems = 0;
    }

    public NmCardDto(MovieCardDto movie, string country)
    {
        Id = movie.Id;
        Title = movie.Title;
        TitleSort = movie.TitleSort;
        Overview = movie.Overview;
        Poster = movie.Poster;
        Backdrop = movie.Backdrop;
        Logo = movie.Logo;
        Year = movie.ReleaseDate.ParseYear();
        Type = MediaTypes.MovieMediaType;
        CreatedAt = movie.CreatedAt;

        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = 1;
        HaveItems = movie.VideoFileCount;

        ColorPalette = ColorPalette.FromJsonOrNull(json: movie.ColorPalette);

        if (movie.CertificationRating != null)
        {
            Rating = new()
            {
                Rating = movie.CertificationRating,
                Iso31661 = movie.CertificationCountry!,
                Image =
                    $"/{movie.CertificationCountry}/{movie.CertificationCountry}_{movie.CertificationRating}.svg",
            };
        }
    }

    public NmCardDto(TvCardDto tv, string country)
    {
        Id = tv.Id;
        Title = tv.Title;
        TitleSort = tv.TitleSort;
        Overview = tv.Overview;
        Poster = tv.Poster;
        Backdrop = tv.Backdrop;
        Logo = tv.Logo;
        Year = tv.FirstAirDate.ParseYear();
        Type = MediaTypes.TvMediaType;
        CreatedAt = tv.CreatedAt;

        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        NumberOfItems = tv.NumberOfEpisodes;
        HaveItems = tv.EpisodesWithVideo;

        ColorPalette = ColorPalette.FromJsonOrNull(json: tv.ColorPalette);

        if (tv.CertificationRating != null)
        {
            Rating = new()
            {
                Rating = tv.CertificationRating,
                Iso31661 = tv.CertificationCountry!,
                Image =
                    $"/{tv.CertificationCountry}/{tv.CertificationCountry}_{tv.CertificationRating}.svg",
            };
        }
    }
}
