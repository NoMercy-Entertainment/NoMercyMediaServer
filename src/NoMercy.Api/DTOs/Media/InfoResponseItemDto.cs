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
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Api.DTOs.Media;

public record InfoResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public long Id { get; set; }

    [JsonProperty(propertyName: "adult")]
    public bool? Adult { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "overview")]
    public string? Overview { get; set; }

    [JsonProperty(propertyName: "poster")]
    public string? Poster { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "logo")]
    public string? Logo { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "watched")]
    public bool Watched { get; set; }

    [JsonProperty(propertyName: "favorite")]
    public bool Favorite { get; set; }

    [JsonProperty(propertyName: "titleSort")]
    public string? TitleSort { get; set; }

    [JsonProperty(propertyName: "duration")]
    public int Duration { get; set; }

    [JsonProperty(propertyName: "number_of_items")]
    public int NumberOfItems { get; set; }

    [JsonProperty(propertyName: "have_items")]
    public int? HaveItems { get; set; }

    [JsonProperty(propertyName: "year")]
    public int Year { get; set; }

    [JsonProperty(propertyName: "voteAverage")]
    public double VoteAverage { get; set; }

    [JsonProperty(propertyName: "external_ids")]
    public ExternalIds? ExternalIds { get; set; }

    [JsonProperty(propertyName: "creator")]
    public PeopleDto? Creator { get; set; }

    [JsonProperty(propertyName: "director")]
    public PeopleDto? Director { get; set; }

    [JsonProperty(propertyName: "writer")]
    public PeopleDto? Writer { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "media_type")]
    public string MediaType { get; set; }

    [JsonProperty(propertyName: "total_duration")]
    public int TotalDuration { get; set; }

    [JsonProperty(propertyName: "genres")]
    public IEnumerable<GenreDto> Genres { get; set; } = [];

    [JsonProperty(propertyName: "keywords")]
    public IEnumerable<string> Keywords { get; set; } = [];

    [JsonProperty(propertyName: "videos")]
    public IEnumerable<VideoDto> Videos { get; set; } = [];

    [JsonProperty(propertyName: "backdrops")]
    public IEnumerable<ImageDto> Backdrops { get; set; } = [];

    [JsonProperty(propertyName: "posters")]
    public IEnumerable<ImageDto> Posters { get; set; } = [];

    [JsonProperty(propertyName: "similar")]
    public IEnumerable<RelatedDto> Similar { get; set; } = [];

    [JsonProperty(propertyName: "recommendations")]
    public IEnumerable<RelatedDto> Recommendations { get; set; } = [];

    [JsonProperty(propertyName: "cast")]
    public IEnumerable<PeopleDto> Cast { get; set; } = [];

    [JsonProperty(propertyName: "crew")]
    public IEnumerable<PeopleDto> Crew { get; set; } = [];

    [JsonProperty(propertyName: "content_ratings")]
    public IEnumerable<ContentRating> ContentRatings { get; set; } = [];

    [JsonProperty(propertyName: "translations")]
    public IEnumerable<TranslationDto> Translations { get; set; } = [];

    [JsonProperty(propertyName: "seasons")]
    public IEnumerable<SeasonDto> Seasons { get; set; } = [];

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "grouped_watch_providers")]
    public IEnumerable<IGrouping<string, WatchProviderDto>> GroupedWatchProviders { get; set; } =
    [];

    [JsonProperty(propertyName: "watch_providers")]
    public IEnumerable<WatchProviderDto> WatchProviders { get; set; } = [];

    [JsonProperty(propertyName: "companies")]
    public IEnumerable<CompanyDto> Companies { get; set; } = [];

    [JsonProperty(propertyName: "networks")]
    public IEnumerable<NetworkDto> Networks { get; set; } = [];

    public InfoResponseItemDto(Movie movie, string? country)
    {
        string? overview = movie.Translations.FirstOrDefault()?.Overview;

        Id = movie.Id;
        Adult = movie.Adult;
        Title = movie.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : movie.Overview;
        Type = MediaTypes.MovieMediaType;
        MediaType = MediaTypes.MovieMediaType;
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        Watched = movie.VideoFiles.Any(predicate: videoFile => videoFile.UserData.Count != 0);

        Favorite = movie.MovieUser.Count != 0;

        TitleSort = movie.Title.TitleSort(date: movie.ReleaseDate);

        Duration =
            movie.VideoFiles.Count != 0
                ? movie
                    .VideoFiles.Select(selector: videoFile => videoFile.Duration?.ToSeconds() ?? 0)
                    .Average()
                    .ToInt()
                : movie.Duration ?? 0;

        Year = movie.ReleaseDate.ParseYear();
        VoteAverage = movie.VoteAverage ?? 0;

        ColorPalette = movie.ColorPalette;
        Backdrop =
            movie
                .Images.FirstOrDefault(predicate: image => image is { Type: "backdrop", Iso6391: null })
                ?.FilePath
            ?? movie.Backdrop;
        Poster = movie.Poster;

        ExternalIds = new() { ImdbId = movie.ImdbId };

        Translations = movie.Translations.Select(selector: translation => new TranslationDto(translation: translation));

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

        Keywords = movie.KeywordMovies.Select(selector: keywordMovie => keywordMovie.Keyword.Name);

        Logo = movie
            .Images.OrderByDescending(keySelector: image => image.VoteAverage)
            .FirstOrDefault(predicate: media => media.Type == "logo")
            ?.FilePath;

        Videos = movie
            .Media.Where(predicate: media => media.Type == "Trailer")
            .Select(selector: media => new VideoDto(media: media));

        Backdrops = movie
            .Images.Where(predicate: media => media.Type == "backdrop")
            .Select(selector: media => new ImageDto(media: media));

        Posters = movie
            .Images.Where(predicate: media => media.Type == "poster")
            .Select(selector: media => new ImageDto(media: media));

        Genres = movie.GenreMovies.Select(selector: genreMovie => new GenreDto(genreMovie: genreMovie));

        PeopleDto[] cast = movie.Cast.Select(selector: cast => new PeopleDto(cast: cast)).ToArray();

        PeopleDto[] crew = movie.Crew.Select(selector: crew => new PeopleDto(crew: crew)).ToArray();

        Cast = cast;
        Crew = crew;

        Director = crew.FirstOrDefault(predicate: people => people.Job == "Director");
        Writer = crew.FirstOrDefault(predicate: people => people.Job == "Writer");

        Similar = movie
            .SimilarFrom.Select(selector: similar => new RelatedDto(similar: similar, type: MediaTypes.MovieMediaType))
            .Where(predicate: related => related.Adult == false)
            .Where(predicate: item => item.Poster != null);

        Recommendations = movie
            .RecommendationFrom.Select(selector: recommendation => new RelatedDto(
                recommendation: recommendation,
                type: MediaTypes.MovieMediaType
            ))
            .Where(predicate: related => related.Adult == false)
            .Where(predicate: item => item.Poster != null);

        GroupedWatchProviders = movie
            .WatchProviderMedia.Select(selector: wpm => new WatchProviderDto(wpm: wpm))
            .GroupBy(keySelector: p => p.ProviderType);

        WatchProviders = movie
            .WatchProviderMedia.DistinctBy(keySelector: wpm => wpm.WatchProviderId)
            .Select(selector: wpm => new WatchProviderDto(wpm: wpm));

        Companies = movie.CompaniesMovies.Select(selector: cm => new CompanyDto(ctv: cm));
    }

    public InfoResponseItemDto(TmdbMovieAppends tmdbMovie, string? country)
    {
        string? overview = tmdbMovie
            .Translations.Translations.FirstOrDefault(predicate: translation =>
                translation.Iso31661 == country
            )
            ?.Data.Overview;

        Id = tmdbMovie.Id;
        Adult = tmdbMovie.Adult;
        Title = tmdbMovie.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : tmdbMovie.Overview;
        Type = MediaTypes.MovieMediaType;
        MediaType = MediaTypes.MovieMediaType;
        Link = new(uriString: $"/movie/{Id}", uriKind: UriKind.Relative);
        Watched = false;

        Favorite = false;

        TitleSort = tmdbMovie.Title.TitleSort(date: tmdbMovie.ReleaseDate);

        Duration = tmdbMovie.Runtime * 60;

        Year = tmdbMovie.ReleaseDate.ParseYear();
        VoteAverage = tmdbMovie.VoteAverage;

        ColorPalette = new();
        Backdrop = tmdbMovie.BackdropPath;
        Poster = tmdbMovie.PosterPath;

        ExternalIds = new() { ImdbId = tmdbMovie.ImdbId };

        Translations = tmdbMovie.Translations.Translations.Select(selector: translation => new TranslationDto(
            translation: translation
        ));

        Keywords = tmdbMovie.Keywords.Results.Select(selector: keywordMovie => keywordMovie.Name);

        Logo = tmdbMovie
            .Images.Logos.OrderByDescending(keySelector: image => image.VoteAverage)
            .FirstOrDefault(predicate: logo => logo.Iso6391 == "en")
            ?.FilePath;

        Videos = tmdbMovie.Videos.Results.Select(selector: media => new VideoDto(media: media));

        Backdrops = tmdbMovie
            .Images.Backdrops.Where(predicate: image => image.Iso6391 is "en" or null)
            .Select(selector: media => new ImageDto(media: media));

        Posters = tmdbMovie
            .Images.Posters.Where(predicate: image => image.Iso6391 is "en" or null)
            .Select(selector: media => new ImageDto(media: media));

        Genres = tmdbMovie.Genres.Select(selector: genreMovie => new GenreDto(tmdbGenreMovie: genreMovie));

        PeopleDto[] cast = tmdbMovie.Credits.Cast.Select(selector: cast => new PeopleDto(tmdbCast: cast)).ToArray();

        PeopleDto[] crew = tmdbMovie.Credits.Crew.Select(selector: crew => new PeopleDto(tmdbCrew: crew)).ToArray();

        Cast = cast;
        Crew = crew;

        Director = crew.FirstOrDefault(predicate: people => people.Job == "Director");
        Writer = crew.FirstOrDefault(predicate: people => people.Job == "Writer");

        Similar = tmdbMovie
            .Similar.Results.Select(selector: similar => new RelatedDto(tmdbSimilar: similar, type: MediaTypes.MovieMediaType))
            .Where(predicate: related => related.Adult == false)
            .Where(predicate: related => related.Poster != null);

        Recommendations = tmdbMovie
            .Recommendations.Results.Select(selector: recommendation => new RelatedDto(
                tmdbSimilar: recommendation,
                type: MediaTypes.MovieMediaType
            ))
            .Where(predicate: related => related.Adult == false)
            .Where(predicate: related => related.Poster != null);

        GroupedWatchProviders = TmdbWatchProviders
            .ExtractProviders(results: tmdbMovie.WatchProviders.TmdbWatchProviderResults)
            .Where(predicate: wpm => wpm.CountryCode == country)
            .Select(selector: wpm => new WatchProviderDto(argKey: wpm))
            .GroupBy(keySelector: p => p.ProviderType);

        WatchProviders = TmdbWatchProviders
            .ExtractProviders(results: tmdbMovie.WatchProviders.TmdbWatchProviderResults)
            .Where(predicate: wpm => wpm.CountryCode == country)
            .DistinctBy(keySelector: wpm => wpm.Provider.ProviderId)
            .Select(selector: wpm => new WatchProviderDto(argKey: wpm));

        Companies = tmdbMovie.ProductionCompanies.Select(selector: cm => new CompanyDto(ctv: cm));
    }

    public InfoResponseItemDto(
        Tv tv,
        string? country,
        Tv[]? similars = null,
        Tv[]? recommendations = null
    )
    {
        string? title = tv.Translations.FirstOrDefault()?.Title;
        string? overview = tv.Translations.FirstOrDefault()?.Overview;

        Id = tv.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : tv.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : tv.Overview;
        Type = tv.Type ?? MediaTypes.TvMediaType;
        MediaType = MediaTypes.TvMediaType;
        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        Watched = tv.Episodes.Any(predicate: episode =>
            episode.VideoFiles.Any(predicate: videoFile => videoFile.UserData.Count != 0)
        );

        Favorite = tv.TvUser.Count != 0;

        TitleSort = tv.Title.TitleSort(date: tv.FirstAirDate);

        Translations = tv.Translations.Select(selector: translation => new TranslationDto(translation: translation));

        Duration = tv
            .Episodes.Where(predicate: episode => episode.EpisodeNumber > 0)
            .SelectMany(selector: episode => episode.VideoFiles)
            .Select(selector: file => file.Duration?.ToSeconds() ?? 0)
            .Sum();

        NumberOfItems = tv.NumberOfEpisodes;
        HaveItems = tv.Episodes.Count(predicate: episode =>
            episode.VideoFiles.Any(predicate: videoFile => videoFile.Folder != null)
        );

        Year = tv.FirstAirDate.ParseYear();
        VoteAverage = tv.VoteAverage ?? 0;

        ColorPalette = tv.ColorPalette;
        Backdrop =
            tv.Images.FirstOrDefault(predicate: image =>
                image is { Type: "backdrop", Iso6391: null }
            )?.FilePath
            ?? tv.Backdrop;
        Poster = tv.Poster;

        ExternalIds = new() { ImdbId = tv.ImdbId, TvdbId = tv.TvdbId };

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

        Keywords = tv.KeywordTvs.Select(selector: keywordTv => keywordTv.Keyword.Name);

        Logo = tv
            .Images.OrderByDescending(keySelector: image => image.VoteAverage)
            .FirstOrDefault(predicate: media => media.Type == "logo")
            ?.FilePath;

        Videos = tv
            .Media.Where(predicate: media => media.Type == "Trailer")
            .Select(selector: media => new VideoDto(media: media));

        Backdrops = tv
            .Images.Where(predicate: media => media.Type == "backdrop")
            .Select(selector: media => new ImageDto(media: media));

        Posters = tv
            .Images.Where(predicate: media => media.Type == "poster")
            .Select(selector: media => new ImageDto(media: media));

        Genres = tv.GenreTvs.Select(selector: genreTv => new GenreDto(genreTv: genreTv));

        ExternalIds = new() { ImdbId = tv.ImdbId, TvdbId = tv.TvdbId };

        PeopleDto[] cast = tv
            .Episodes.SelectMany(selector: episode => episode.Cast)
            .Concat(second: tv.Cast)
            .Select(selector: cast => new PeopleDto(cast: cast))
            .GroupBy(keySelector: people => people.Id)
            .Select(selector: group => group.First())
            .ToArray();

        PeopleDto[] crew = tv
            .Episodes.SelectMany(selector: episode => episode.Crew)
            .Concat(second: tv.Crew)
            .Select(selector: crew => new PeopleDto(crew: crew))
            .GroupBy(keySelector: people => people.Id)
            .Select(selector: group => group.First())
            .ToArray();

        Cast = cast;
        Crew = crew;
        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        Director = crew.FirstOrDefault(predicate: people => people.Job == "Director");
        Writer = crew.FirstOrDefault(predicate: people => people.Job == "Writer");

        Creator = tv.Creators.Select(selector: people => new PeopleDto(creator: people)).FirstOrDefault();

        // Detail-only enrichment: the lite callers pass no related arrays and must
        // not get seasons / watch providers / networks / companies populated.
        if (similars is null && recommendations is null)
            return;

        Similar = tv
            .SimilarFrom.Select(selector: similar => new RelatedDto(
                similar: similar,
                type: MediaTypes.TvMediaType,
                similars: similars ?? []
            ))
            .Where(predicate: item => item.Poster != null);

        Recommendations = tv
            .RecommendationFrom.Select(selector: recommendation => new RelatedDto(
                recommendation: recommendation,
                type: MediaTypes.TvMediaType,
                recommendations: recommendations ?? []
            ))
            .Where(predicate: item => item.Poster != null);

        Seasons = tv
            .Seasons.OrderBy(keySelector: season => season.SeasonNumber)
            .Select(selector: season => new SeasonDto(season: season));

        GroupedWatchProviders = tv
            .WatchProviderMedia.Select(selector: wpm => new WatchProviderDto(wpm: wpm))
            .GroupBy(keySelector: p => p.ProviderType);

        WatchProviders = tv
            .WatchProviderMedia.DistinctBy(keySelector: wpm => wpm.WatchProviderId)
            .Select(selector: wpm => new WatchProviderDto(wpm: wpm));

        Networks = tv.NetworkTvs.Select(selector: ntv => new NetworkDto(ntv: ntv));

        Companies = tv.CompaniesTvs.Select(selector: ctv => new CompanyDto(ctv: ctv));
    }

    public InfoResponseItemDto(TmdbTvShowAppends tmdbTv, string? country)
    {
        string? title = tmdbTv
            .Translations.Translations.FirstOrDefault(predicate: translation =>
                translation.Iso31661 == country
            )
            ?.Data.Title;

        string? overview = tmdbTv
            .Translations.Translations.FirstOrDefault(predicate: translation =>
                translation.Iso31661 == country
            )
            ?.Data.Overview;

        Id = tmdbTv.Id;
        Adult = tmdbTv.Adult;
        Title = !string.IsNullOrEmpty(value: title) ? title : tmdbTv.Name;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : tmdbTv.Overview;
        Type = tmdbTv.Type ?? MediaTypes.TvMediaType;
        MediaType = MediaTypes.TvMediaType;
        Link = new(uriString: $"/tv/{Id}", uriKind: UriKind.Relative);
        Watched = false;
        Favorite = false;

        TitleSort = tmdbTv.Name.TitleSort(date: tmdbTv.FirstAirDate);

        Translations = tmdbTv.Translations.Translations.Select(selector: translation => new TranslationDto(
            translation: translation
        ));

        Duration =
            tmdbTv.EpisodeRunTime?.Length > 0
                ? (tmdbTv.EpisodeRunTime.Average() * tmdbTv.NumberOfEpisodes).ToInt() * 60
                : 0;

        NumberOfItems = tmdbTv.NumberOfEpisodes;
        HaveItems = 0;
        Year = tmdbTv.FirstAirDate.ParseYear();
        VoteAverage = tmdbTv.VoteAverage;

        // ColorPalette = tv.ColorPalette;
        Backdrop =
            tmdbTv.Images.Backdrops.FirstOrDefault(predicate: media => media.Iso6391 is "")?.FilePath
            ?? tmdbTv.BackdropPath;

        Poster =
            tmdbTv.Images.Posters.FirstOrDefault(predicate: poster => poster.Iso6391 is "")?.FilePath
            ?? tmdbTv.PosterPath;

        ExternalIds = new()
        {
            ImdbId = tmdbTv.ExternalIds.ImdbId,
            TvdbId = tmdbTv.ExternalIds.TvdbId,
        };

        ContentRatings = tmdbTv
            .ContentRatings.Results.Where(predicate: certificationMovie =>
                certificationMovie.Iso31661 == "US" || certificationMovie.Iso31661 == country
            )
            .Select(selector: certificationTv => new ContentRating
            {
                Rating = certificationTv.Rating,
                Iso31661 = certificationTv.Iso31661,
            });

        Keywords = tmdbTv.Keywords.Results.Select(selector: keywordTv => keywordTv.Name);

        Logo = tmdbTv
            .Images.Logos.OrderByDescending(keySelector: image => image.VoteAverage)
            .FirstOrDefault(predicate: media => media.Iso6391 == "en")
            ?.FilePath;

        Videos = tmdbTv.Videos.Results.Select(selector: media => new VideoDto(media: media));

        Backdrops = tmdbTv
            .Images.Backdrops.Where(predicate: image => image.Iso6391 is "en" or null)
            .Select(selector: media => new ImageDto(media: media));

        Posters = tmdbTv
            .Images.Posters.Where(predicate: image => image.Iso6391 is "en" or null)
            .Select(selector: media => new ImageDto(media: media));

        Genres = tmdbTv.Genres.Select(selector: genreTv => new GenreDto(tmdbGenreMovie: genreTv));

        PeopleDto[] cast = tmdbTv.Credits.Cast.Select(selector: cast => new PeopleDto(tmdbCast: cast)).ToArray();

        PeopleDto[] crew = tmdbTv.Credits.Crew.Select(selector: crew => new PeopleDto(tmdbCrew: crew)).ToArray();

        Cast = cast;
        Crew = crew;

        Director = crew.FirstOrDefault(predicate: people => people.Job == "Director");
        Writer = crew.FirstOrDefault(predicate: people => people.Job == "Writer");

        Creator = tmdbTv.CreatedBy.Select(selector: people => new PeopleDto(crew: people)).FirstOrDefault();

        Similar = tmdbTv
            .Similar.Results.Select(selector: similar => new RelatedDto(recommendation: similar, type: MediaTypes.TvMediaType))
            .Where(predicate: item => item.Poster != null);

        Recommendations = tmdbTv
            .Recommendations.Results.Select(selector: recommendation => new RelatedDto(
                recommendation: recommendation,
                type: MediaTypes.TvMediaType
            ))
            .Where(predicate: item => item.Poster != null);

        Seasons = [];

        GroupedWatchProviders = TmdbWatchProviders
            .ExtractProviders(results: tmdbTv.WatchProviders.TmdbWatchProviderResults)
            .Where(predicate: wpm => wpm.CountryCode == country)
            .Select(selector: wpm => new WatchProviderDto(argKey: wpm))
            .GroupBy(keySelector: p => p.ProviderType);

        WatchProviders = TmdbWatchProviders
            .ExtractProviders(results: tmdbTv.WatchProviders.TmdbWatchProviderResults)
            .Where(predicate: wpm => wpm.CountryCode == country)
            .DistinctBy(keySelector: wpm => wpm.Provider.ProviderId)
            .Select(selector: wpm => new WatchProviderDto(argKey: wpm));

        Companies = tmdbTv.ProductionCompanies.Select(selector: cm => new CompanyDto(ctv: cm));

        Networks = tmdbTv.Networks.Select(selector: ntv => new NetworkDto(ntv: ntv));
    }

    public InfoResponseItemDto(Collection collection, string country)
    {
        string? title = collection.Translations.FirstOrDefault()?.Title;

        string? overview = collection.Translations.FirstOrDefault()?.Overview;

        Id = collection.Id;
        Title = !string.IsNullOrEmpty(value: title) ? title : collection.Title;
        Overview = !string.IsNullOrEmpty(value: overview) ? overview : collection.Overview;
        Type = MediaTypes.CollectionMediaType;
        MediaType = MediaTypes.CollectionMediaType;
        Link = new(uriString: $"/collection/{Id}", uriKind: UriKind.Relative);
        // Watched = tv.Watched;
        // Favorite = tv.Favorite;
        TitleSort = collection.Title.TitleSort(
            parseYear: collection
                .CollectionMovies.MinBy(keySelector: collectionMovie => collectionMovie.Movie.ReleaseDate)
                ?.Movie.ReleaseDate.ParseYear()
        );

        Duration = collection
            .CollectionMovies.SelectMany(selector: collectionMovie => collectionMovie.Movie.VideoFiles)
            .Select(selector: videoFile => videoFile.Duration?.ToSeconds() ?? 0)
            .Sum();

        Translations = collection.Translations.Select(selector: translation => new TranslationDto(
            translation: translation
        ));

        Year =
            collection
                .CollectionMovies.MinBy(keySelector: collectionMovie => collectionMovie.Movie.ReleaseDate)
                ?.Movie.ReleaseDate.ParseYear()
            ?? 0;

        VoteAverage =
            collection.CollectionMovies.Average(selector: collectionMovie =>
                collectionMovie.Movie.VoteAverage
            ) ?? 0;

        ColorPalette = collection.ColorPalette;
        Backdrop =
            collection
                .Images.FirstOrDefault(predicate: image => image is { Type: "backdrop", Iso6391: null })
                ?.FilePath
            ?? collection.Backdrop;
        Poster =
            collection
                .Images.FirstOrDefault(predicate: image => image is { Type: "poster", Iso6391: null })
                ?.FilePath
            ?? collection.Poster;

        ContentRatings = collection.CollectionMovies.Select(selector: certificationMovie => new ContentRating
        {
            Rating = certificationMovie
                .Movie.CertificationMovies.First(predicate: cert =>
                    cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country
                )
                .Certification.Rating,
            Iso31661 = certificationMovie
                .Movie.CertificationMovies.First(predicate: cert =>
                    cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country
                )
                .Certification.Iso31661,
        });

        Keywords = collection
            .CollectionMovies.SelectMany(selector: collectionMovie => collectionMovie.Movie.KeywordMovies)
            .Select(selector: keywordMovie => keywordMovie.Keyword.Name);

        Logo = collection
            .CollectionMovies.Select(selector: collectionMovie =>
                collectionMovie
                    .Movie.Images.OrderByDescending(keySelector: image => image.VoteAverage)
                    .FirstOrDefault(predicate: media => media.Type == "logo")
                    ?.FilePath
            )
            .FirstOrDefault();

        PeopleDto[] cast = collection
            .CollectionMovies.SelectMany(selector: collectionMovie => collectionMovie.Movie.Cast)
            .Where(predicate: cast => cast.Person.Adult == false)
            .Select(selector: cast => new PeopleDto(cast: cast))
            .ToArray();

        PeopleDto[] crew = collection
            .CollectionMovies.SelectMany(selector: collectionMovie => collectionMovie.Movie.Crew)
            .Where(predicate: crew => crew.Person.Adult == false)
            .Select(selector: crew => new PeopleDto(crew: crew))
            .ToArray();

        Cast = cast;
        Crew = crew;

        Director = crew.FirstOrDefault(predicate: people => people.Job == "Director");

        Writer = crew.FirstOrDefault(predicate: people => people.Job == "Writer");

        GroupedWatchProviders = collection
            .CollectionMovies.SelectMany(selector: cm => cm.Movie.WatchProviderMedia)
            .Select(selector: wpm => new WatchProviderDto(wpm: wpm))
            .GroupBy(keySelector: p => p.ProviderType);

        WatchProviders = collection
            .CollectionMovies.SelectMany(selector: cm => cm.Movie.WatchProviderMedia)
            .DistinctBy(keySelector: wpm => wpm.WatchProviderId)
            .Select(selector: wpm => new WatchProviderDto(wpm: wpm));

        Companies = collection
            .CollectionMovies.SelectMany(selector: cm => cm.Movie.CompaniesMovies)
            .Select(selector: ctv => new CompanyDto(ctv: ctv));
    }
}
