using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Api.DTOs.Media;

public record InfoResponseItemDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("logo")] public string? Logo { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("watched")] public bool Watched { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("duration")] public int Duration { get; set; }
    [JsonProperty("number_of_items")] public int NumberOfItems { get; set; }
    [JsonProperty("have_items")] public int? HaveItems { get; set; }
    [JsonProperty("year")] public int Year { get; set; }
    [JsonProperty("voteAverage")] public double VoteAverage { get; set; }
    [JsonProperty("external_ids")] public ExternalIds? ExternalIds { get; set; }
    [JsonProperty("creator")] public PeopleDto? Creator { get; set; }
    [JsonProperty("director")] public PeopleDto? Director { get; set; }
    [JsonProperty("writer")] public PeopleDto? Writer { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("media_type")] public string MediaType { get; set; }
    [JsonProperty("total_duration")] public int TotalDuration { get; set; }

    [JsonProperty("genres")] public IEnumerable<GenreDto> Genres { get; set; } = [];
    [JsonProperty("keywords")] public IEnumerable<string> Keywords { get; set; } = [];
    [JsonProperty("videos")] public IEnumerable<VideoDto> Videos { get; set; } = [];
    [JsonProperty("backdrops")] public IEnumerable<ImageDto> Backdrops { get; set; } = [];
    [JsonProperty("posters")] public IEnumerable<ImageDto> Posters { get; set; } = [];
    [JsonProperty("similar")] public IEnumerable<RelatedDto> Similar { get; set; } = [];
    [JsonProperty("recommendations")] public IEnumerable<RelatedDto> Recommendations { get; set; } = [];
    [JsonProperty("cast")] public IEnumerable<PeopleDto> Cast { get; set; } = [];
    [JsonProperty("crew")] public IEnumerable<PeopleDto> Crew { get; set; } = [];
    [JsonProperty("content_ratings")] public IEnumerable<ContentRating> ContentRatings { get; set; } = [];
    [JsonProperty("translations")] public IEnumerable<TranslationDto> Translations { get; set; } = [];
    [JsonProperty("seasons")] public IEnumerable<SeasonDto> Seasons { get; set; } = [];
    [JsonProperty("link")] public Uri Link { get; set; } = null!;

    [JsonProperty("grouped_watch_providers")]
    public IEnumerable<IGrouping<string, WatchProviderDto>> GroupedWatchProviders { get; set; } = [];

    [JsonProperty("watch_providers")] public IEnumerable<WatchProviderDto> WatchProviders { get; set; } = [];
    [JsonProperty("companies")] public IEnumerable<CompanyDto> Companies { get; set; } = [];
    [JsonProperty("networks")] public IEnumerable<NetworkDto> Networks { get; set; } = [];

    public InfoResponseItemDto(Movie movie, string? country)
    {
        string? overview = movie.Translations.FirstOrDefault()?.Overview;

        Id = movie.Id;
        Title = movie.Title;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : movie.Overview;
        Type = Config.MovieMediaType;
        MediaType = Config.MovieMediaType;
        Link = new($"/movie/{Id}", UriKind.Relative);
        Watched = movie.VideoFiles
            .Any(videoFile => videoFile.UserData.Count != 0);

        Favorite = movie.MovieUser.Count != 0;

        TitleSort = movie.Title.TitleSort(movie.ReleaseDate);

        Duration = movie.VideoFiles.Count != 0
            ? movie.VideoFiles.Select(videoFile => videoFile.Duration?.ToSeconds() ?? 0).Average().ToInt()
            : movie.Duration ?? 0;

        Year = movie.ReleaseDate.ParseYear();
        VoteAverage = movie.VoteAverage ?? 0;

        ColorPalette = movie.ColorPalette;
        Backdrop = movie.Images.FirstOrDefault(image => image is { Type: "backdrop", Iso6391: null })?.FilePath ??
                   movie.Backdrop;
        Poster = movie.Poster;

        ExternalIds = new()
        {
            ImdbId = movie.ImdbId
        };

        Translations = movie.Translations
            .Select(translation => new TranslationDto(translation));

        ContentRatings = movie.CertificationMovies
            .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US"
                                         || certificationMovie.Certification.Iso31661 == country)
            .Select(certificationMovie => new ContentRating
            {
                Rating = certificationMovie.Certification.Rating,
                Iso31661 = certificationMovie.Certification.Iso31661
            });

        Keywords = movie.KeywordMovies
            .Select(keywordMovie => keywordMovie.Keyword.Name);

        Logo = movie.Images
            .OrderByDescending(image => image.VoteAverage)
            .FirstOrDefault(media => media.Type == "logo")?.FilePath;

        Videos = movie.Media
            .Where(media => media.Type == "Trailer")
            .Select(media => new VideoDto(media));

        Backdrops = movie.Images
            .Where(media => media.Type == "backdrop")
            .Select(media => new ImageDto(media));

        Posters = movie.Images
            .Where(media => media.Type == "poster")
            .Select(media => new ImageDto(media));

        Genres = movie.GenreMovies
            .Select(genreMovie => new GenreDto(genreMovie));

        PeopleDto[] cast = movie.Cast
            .Select(cast => new PeopleDto(cast))
            .ToArray();

        PeopleDto[] crew = movie.Crew
            .Select(crew => new PeopleDto(crew))
            .ToArray();

        Cast = cast;
        Crew = crew;

        Director = crew.FirstOrDefault(people => people.Job == "Director");
        Writer = crew.FirstOrDefault(people => people.Job == "Writer");

        Similar = movie.SimilarFrom
            .Select(similar => new RelatedDto(similar, Config.MovieMediaType))
            .Where(item => item.Poster != null);

        Recommendations = movie.RecommendationFrom
            .Select(recommendation => new RelatedDto(recommendation, Config.MovieMediaType))
            .Where(item => item.Poster != null);

        GroupedWatchProviders = movie.WatchProviderMedia
            .Select(wpm => new WatchProviderDto(wpm))
            .GroupBy(p => p.ProviderType);

        WatchProviders = movie.WatchProviderMedia
            .DistinctBy(wpm => wpm.WatchProviderId)
            .Select(wpm => new WatchProviderDto(wpm));

        Companies = movie.CompaniesMovies
            .Select(cm => new CompanyDto(cm));
    }

    public InfoResponseItemDto(TmdbMovieAppends tmdbMovie, string? country)
    {
        string? overview = tmdbMovie.Translations.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Data.Overview;

        Id = tmdbMovie.Id;
        Title = tmdbMovie.Title;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : tmdbMovie.Overview;
        Type = Config.MovieMediaType;
        MediaType = Config.MovieMediaType;
        Link = new($"/movie/{Id}", UriKind.Relative);
        Watched = false;

        Favorite = false;

        TitleSort = tmdbMovie.Title.TitleSort(tmdbMovie.ReleaseDate);

        Duration = tmdbMovie.Runtime * 60;

        Year = tmdbMovie.ReleaseDate.ParseYear();
        VoteAverage = tmdbMovie.VoteAverage;

        ColorPalette = new();
        Backdrop = tmdbMovie.BackdropPath;
        Poster = tmdbMovie.PosterPath;

        ExternalIds = new()
        {
            ImdbId = tmdbMovie.ImdbId
        };

        Translations = tmdbMovie.Translations.Translations
            .Select(translation => new TranslationDto(translation));

        Keywords = tmdbMovie.Keywords.Results
            .Select(keywordMovie => keywordMovie.Name);

        Logo = tmdbMovie.Images.Logos
            .OrderByDescending(image => image.VoteAverage)
            .FirstOrDefault(logo => logo.Iso6391 == "en")?.FilePath;

        Videos = tmdbMovie.Videos.Results
            .Select(media => new VideoDto(media));

        Backdrops = tmdbMovie.Images.Backdrops
            .Where(image => image.Iso6391 is "en" or null)
            .Select(media => new ImageDto(media));

        Posters = tmdbMovie.Images.Posters
            .Where(image => image.Iso6391 is "en" or null)
            .Select(media => new ImageDto(media));

        Genres = tmdbMovie.Genres
            .Select(genreMovie => new GenreDto(genreMovie));

        PeopleDto[] cast = tmdbMovie.Credits.Cast
            .Select(cast => new PeopleDto(cast))
            .ToArray();

        PeopleDto[] crew = tmdbMovie.Credits.Crew
            .Select(crew => new PeopleDto(crew))
            .ToArray();

        Cast = cast;
        Crew = crew;

        Director = crew.FirstOrDefault(people => people.Job == "Director");
        Writer = crew.FirstOrDefault(people => people.Job == "Writer");

        Similar = tmdbMovie.Similar.Results
            .Select(similar => new RelatedDto(similar, Config.MovieMediaType))
            .Where(item => item.Poster != null);

        Recommendations = tmdbMovie.Recommendations.Results
            .Select(recommendation => new RelatedDto(recommendation, Config.MovieMediaType))
            .Where(item => item.Poster != null);

        GroupedWatchProviders = TmdbWatchProviders.ExtractProviders(tmdbMovie.WatchProviders.TmdbWatchProviderResults)
            .Where(wpm => wpm.CountryCode == country)
            .Select(wpm => new WatchProviderDto(wpm))
            .GroupBy(p => p.ProviderType);

        WatchProviders = TmdbWatchProviders.ExtractProviders(tmdbMovie.WatchProviders.TmdbWatchProviderResults)
            .Where(wpm => wpm.CountryCode == country)
            .DistinctBy(wpm => wpm.Provider.ProviderId)
            .Select(wpm => new WatchProviderDto(wpm));

        Companies = tmdbMovie.ProductionCompanies
            .Select(cm => new CompanyDto(cm));
    }

    public InfoResponseItemDto(Tv tv, string? country, MediaContext? mediaContext = null)
    {
        string? title = tv.Translations.FirstOrDefault()?.Title;
        string? overview = tv.Translations.FirstOrDefault()?.Overview;

        Id = tv.Id;
        Title = !string.IsNullOrEmpty(title)
            ? title
            : tv.Title;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : tv.Overview;
        Type = tv.Type ?? Config.TvMediaType;
        MediaType = Config.TvMediaType;
        Link = new($"/tv/{Id}", UriKind.Relative);
        Watched = tv.Episodes
            .Any(episode => episode.VideoFiles
                .Any(videoFile => videoFile.UserData.Count != 0));

        Favorite = tv.TvUser.Count != 0;

        TitleSort = tv.Title.TitleSort(tv.FirstAirDate);

        Translations = tv.Translations
            .Select(translation => new TranslationDto(translation));

        Duration = tv.Episodes
            .Where(episode => episode.EpisodeNumber > 0)
            .SelectMany(episode => episode.VideoFiles)
            .Select(file => file.Duration?.ToSeconds() ?? 0)
            .Sum();

        NumberOfItems = tv.NumberOfEpisodes;
        HaveItems = tv.Episodes.Count(episode => episode.VideoFiles.Any(videoFile => videoFile.Folder != null));

        Year = tv.FirstAirDate.ParseYear();
        VoteAverage = tv.VoteAverage ?? 0;

        ColorPalette = tv.ColorPalette;
        Backdrop = tv.Images.FirstOrDefault(image => image is { Type: "backdrop", Iso6391: null })?.FilePath ??
                   tv.Backdrop;
        Poster = tv.Poster;

        ExternalIds = new()
        {
            ImdbId = tv.ImdbId,
            TvdbId = tv.TvdbId
        };

        ContentRatings = tv.CertificationTvs
            .Where(certificationMovie => certificationMovie.Certification.Iso31661 == "US"
                                         || certificationMovie.Certification.Iso31661 == country)
            .Select(certificationTv => new ContentRating
            {
                Rating = certificationTv.Certification.Rating,
                Iso31661 = certificationTv.Certification.Iso31661
            });

        Keywords = tv.KeywordTvs
            .Select(keywordTv => keywordTv.Keyword.Name);

        Logo = tv.Images
            .OrderByDescending(image => image.VoteAverage)
            .FirstOrDefault(media => media.Type == "logo")?.FilePath;

        Videos = tv.Media
            .Where(media => media.Type == "Trailer")
            .Select(media => new VideoDto(media));

        Backdrops = tv.Images
            .Where(media => media.Type == "backdrop")
            .Select(media => new ImageDto(media));

        Posters = tv.Images
            .Where(media => media.Type == "poster")
            .Select(media => new ImageDto(media));

        Genres = tv.GenreTvs
            .Select(genreTv => new GenreDto(genreTv));

        ExternalIds = new()
        {
            ImdbId = tv.ImdbId,
            TvdbId = tv.TvdbId
        };

        PeopleDto[] cast = tv.Episodes
            .SelectMany(episode => episode.Cast)
            .Concat(tv.Cast)
            .Select(cast => new PeopleDto(cast))
            .GroupBy(people => people.Id)
            .Select(group => group.First())
            .ToArray();

        PeopleDto[] crew = tv.Episodes
            .SelectMany(episode => episode.Crew)
            .Concat(tv.Crew)
            .Select(crew => new PeopleDto(crew))
            .GroupBy(people => people.Id)
            .Select(group => group.First())
            .ToArray();

        Cast = cast;
        Crew = crew;
        Link = new($"/tv/{Id}", UriKind.Relative);
        Director = crew.FirstOrDefault(people => people.Job == "Director");
        Writer = crew.FirstOrDefault(people => people.Job == "Writer");

        Creator = tv.Creators
            .Select(people => new PeopleDto(people)).FirstOrDefault();

        if (mediaContext is null) return;

        IEnumerable<int> similarIds = tv.SimilarFrom
            .Select(similar => similar.MediaId);
        Tv[] similars = mediaContext.Tvs
            .Where(t => similarIds.Contains(t.Id))
            .Include(t => t.Episodes)
            .ThenInclude(episode => episode.VideoFiles)
            .ToArray();
        Similar = tv.SimilarFrom
            .Select(similar => new RelatedDto(similar, Config.TvMediaType, similars))
            .Where(item => item.Poster != null);

        IEnumerable<int> recommendationIds = tv.RecommendationFrom
            .Select(recommendation => recommendation.MediaId);
        Tv[] recommendations = mediaContext.Tvs
            .Where(t => recommendationIds.Contains(t.Id))
            .Include(t => t.Episodes)
            .ThenInclude(episode => episode.VideoFiles)
            .ToArray();

        Recommendations = tv.RecommendationFrom
            .Select(recommendation => new RelatedDto(recommendation, Config.TvMediaType, recommendations))
            .Where(item => item.Poster != null);

        Seasons = tv.Seasons
            .OrderBy(season => season.SeasonNumber)
            .Select(season => new SeasonDto(season));

        GroupedWatchProviders = tv.WatchProviderMedia
            .Select(wpm => new WatchProviderDto(wpm))
            .GroupBy(p => p.ProviderType);

        WatchProviders = tv.WatchProviderMedia
            .DistinctBy(wpm => wpm.WatchProviderId)
            .Select(wpm => new WatchProviderDto(wpm));

        Networks = tv.NetworkTvs
            .Select(ntv => new NetworkDto(ntv));

        Companies = tv.CompaniesTvs
            .Select(ctv => new CompanyDto(ctv));
    }

    public InfoResponseItemDto(TmdbTvShowAppends tmdbTv, string? country)
    {
        string? title = tmdbTv.Translations.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Data.Title;

        string? overview = tmdbTv.Translations.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?
            .Data.Overview;

        Id = tmdbTv.Id;
        Title = !string.IsNullOrEmpty(title)
            ? title
            : tmdbTv.Name;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : tmdbTv.Overview;
        Type = tmdbTv.Type ?? Config.TvMediaType;
        MediaType = Config.TvMediaType;
        Link = new($"/tv/{Id}", UriKind.Relative);
        Watched = false;
        Favorite = false;

        TitleSort = tmdbTv.Name.TitleSort(tmdbTv.FirstAirDate);

        Translations = tmdbTv.Translations.Translations
            .Select(translation => new TranslationDto(translation));

        Duration = tmdbTv.EpisodeRunTime?.Length > 0
            ? (tmdbTv.EpisodeRunTime.Average() * tmdbTv.NumberOfEpisodes).ToInt() * 60
            : 0;

        NumberOfItems = tmdbTv.NumberOfEpisodes;
        HaveItems = 0;
        Year = tmdbTv.FirstAirDate.ParseYear();
        VoteAverage = tmdbTv.VoteAverage;

        // ColorPalette = tv.ColorPalette;
        Backdrop = tmdbTv.Images.Backdrops.FirstOrDefault(media => media.Iso6391 is "")?.FilePath ??
                   tmdbTv.BackdropPath;

        Poster = tmdbTv.Images.Posters.FirstOrDefault(poster => poster.Iso6391 is "")?.FilePath ??
                 tmdbTv.PosterPath;


        ExternalIds = new()
        {
            ImdbId = tmdbTv.ExternalIds.ImdbId,
            TvdbId = tmdbTv.ExternalIds.TvdbId
        };

        ContentRatings = tmdbTv.ContentRatings.Results
            .Where(certificationMovie => certificationMovie.Iso31661 == "US" || certificationMovie.Iso31661 == country)
            .Select(certificationTv => new ContentRating
            {
                Rating = certificationTv.Rating,
                Iso31661 = certificationTv.Iso31661
            });

        Keywords = tmdbTv.Keywords.Results
            .Select(keywordTv => keywordTv.Name);

        Logo = tmdbTv.Images.Logos
            .OrderByDescending(image => image.VoteAverage)
            .FirstOrDefault(media => media.Iso6391 == "en")?.FilePath;

        Videos = tmdbTv.Videos.Results
            .Select(media => new VideoDto(media));

        Backdrops = tmdbTv.Images.Backdrops
            .Where(image => image.Iso6391 is "en" or null)
            .Select(media => new ImageDto(media));

        Posters = tmdbTv.Images.Posters
            .Where(image => image.Iso6391 is "en" or null)
            .Select(media => new ImageDto(media));

        Genres = tmdbTv.Genres
            .Select(genreTv => new GenreDto(genreTv));

        PeopleDto[] cast = tmdbTv.Credits.Cast
            .Select(cast => new PeopleDto(cast))
            .ToArray();

        PeopleDto[] crew = tmdbTv.Credits.Crew
            .Select(crew => new PeopleDto(crew))
            .ToArray();

        Cast = cast;
        Crew = crew;

        Director = crew.FirstOrDefault(people => people.Job == "Director");
        Writer = crew.FirstOrDefault(people => people.Job == "Writer");

        Creator = tmdbTv.CreatedBy
            .Select(people => new PeopleDto(people)).FirstOrDefault();

        Similar = tmdbTv.Similar.Results
            .Select(similar => new RelatedDto(similar, Config.TvMediaType))
            .Where(item => item.Poster != null);

        Recommendations = tmdbTv.Recommendations.Results
            .Select(recommendation => new RelatedDto(recommendation, Config.TvMediaType))
            .Where(item => item.Poster != null);

        Seasons = [];

        GroupedWatchProviders = TmdbWatchProviders.ExtractProviders(tmdbTv.WatchProviders.TmdbWatchProviderResults)
            .Where(wpm => wpm.CountryCode == country)
            .Select(wpm => new WatchProviderDto(wpm))
            .GroupBy(p => p.ProviderType);

        WatchProviders = TmdbWatchProviders.ExtractProviders(tmdbTv.WatchProviders.TmdbWatchProviderResults)
            .Where(wpm => wpm.CountryCode == country)
            .DistinctBy(wpm => wpm.Provider.ProviderId)
            .Select(wpm => new WatchProviderDto(wpm));

        Companies = tmdbTv.ProductionCompanies
            .Select(cm => new CompanyDto(cm));
        
        Networks = tmdbTv.Networks
            .Select(ntv => new NetworkDto(ntv));
    }

    public InfoResponseItemDto(Collection collection, string country)
    {
        string? title = collection.Translations
            .FirstOrDefault()?.Title;

        string? overview = collection.Translations
            .FirstOrDefault()?.Overview;

        Id = collection.Id;
        Title = !string.IsNullOrEmpty(title)
            ? title
            : collection.Title;
        Overview = !string.IsNullOrEmpty(overview)
            ? overview
            : collection.Overview;
        Type = Config.CollectionMediaType;
        MediaType = Config.CollectionMediaType;
        Link = new($"/collection/{Id}", UriKind.Relative);
        // Watched = tv.Watched;
        // Favorite = tv.Favorite;
        TitleSort = collection.Title
            .TitleSort(collection.CollectionMovies
                .MinBy(collectionMovie => collectionMovie.Movie.ReleaseDate)
                ?.Movie.ReleaseDate
                .ParseYear());

        Duration = collection.CollectionMovies
            .SelectMany(collectionMovie => collectionMovie.Movie.VideoFiles)
            .Select(videoFile => videoFile.Duration?.ToSeconds() ?? 0)
            .Sum();

        Translations = collection.Translations
            .Select(translation => new TranslationDto(translation));

        Year = collection.CollectionMovies
            .MinBy(collectionMovie => collectionMovie.Movie.ReleaseDate)
            ?.Movie.ReleaseDate
            .ParseYear() ?? 0;

        VoteAverage = collection.CollectionMovies
            .Average(collectionMovie => collectionMovie.Movie.VoteAverage) ?? 0;

        ColorPalette = collection.ColorPalette;
        Backdrop = collection.Images.FirstOrDefault(image => image is { Type: "backdrop", Iso6391: null })?.FilePath ??
                   collection.Backdrop;
        Poster = collection.Images.FirstOrDefault(image => image is { Type: "poster", Iso6391: null })?.FilePath ??
                 collection.Poster;

        ContentRatings = collection.CollectionMovies
            .Select(certificationMovie => new ContentRating
            {
                Rating = certificationMovie.Movie.CertificationMovies
                    .First(cert => cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country)
                    .Certification.Rating,
                Iso31661 = certificationMovie.Movie.CertificationMovies
                    .First(cert => cert.Certification.Iso31661 == "US" || cert.Certification.Iso31661 == country)
                    .Certification.Iso31661
            });

        Keywords = collection.CollectionMovies
            .SelectMany(collectionMovie => collectionMovie.Movie.KeywordMovies)
            .Select(keywordMovie => keywordMovie.Keyword.Name);

        Logo = collection.CollectionMovies
            .Select(collectionMovie => collectionMovie.Movie.Images
                .OrderByDescending(image => image.VoteAverage)
                .FirstOrDefault(media => media.Type == "logo")?.FilePath)
            .FirstOrDefault();

        PeopleDto[] cast = collection.CollectionMovies
            .SelectMany(collectionMovie => collectionMovie.Movie.Cast)
            .Select(cast => new PeopleDto(cast))
            .ToArray();

        PeopleDto[] crew = collection.CollectionMovies
            .SelectMany(collectionMovie => collectionMovie.Movie.Crew)
            .Select(crew => new PeopleDto(crew))
            .ToArray();

        Cast = cast;
        Crew = crew;

        Director = crew.FirstOrDefault(people => people.Job == "Director");

        Writer = crew.FirstOrDefault(people => people.Job == "Writer");

        GroupedWatchProviders = collection.CollectionMovies
            .SelectMany(cm => cm.Movie.WatchProviderMedia)
            .Select(wpm => new WatchProviderDto(wpm))
            .GroupBy(p => p.ProviderType);

        WatchProviders = collection.CollectionMovies
            .SelectMany(cm => cm.Movie.WatchProviderMedia)
            .DistinctBy(wpm => wpm.WatchProviderId)
            .Select(wpm => new WatchProviderDto(wpm));

        Companies = collection.CollectionMovies
            .SelectMany(cm => cm.Movie.CompaniesMovies)
            .Select(ctv => new CompanyDto(ctv));
    }
}