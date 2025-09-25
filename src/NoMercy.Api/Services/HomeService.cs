using NoMercy.Api.Controllers.V1.Media;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.NmSystem;
using NoMercy.Data.Repositories;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Services;

public class HomeService
{
    private const int MaximumCardsInCarousel = 36;
    private const int MaximumItemsPerPage = 300;
    private const string TvMediaType = "tv";
    private const string MovieMediaType = "movie";
    private const string AnimeMediaType = "anime";

    private readonly MediaContext _mediaContext;
    private readonly LibraryRepository _libraryRepository;
    private readonly HomeRepository _homeRepository;


    public HomeService(HomeRepository homeRepository, LibraryRepository libraryRepository, MediaContext mediaContext)
    {
        _homeRepository = homeRepository;
        _libraryRepository = libraryRepository;
        _mediaContext = mediaContext;
    }

    public async Task<List<GenreRowDto<GenreRowItemDto>>> GetHomePageContent(
        Guid userId,
        string language,
        string country,
        PageRequestDto request)
    {
        List<Genre> genreItems = await HomeResponseDto
            .GetHome(_mediaContext, userId, language, request.Take, request.Page);

        List<GenreRowDto<GenreRowItemDto>> genres = FetchGenres(genreItems)
            .ToList();

        List<Tv> tvData = await FetchTvData(language, genres);

        List<Movie> movieData = await FetchMovieData(language, genres);

        foreach (GenreRowDto<GenreRowItemDto> genre in genres)
        {
            genre.Items = genre.Source
                .Select(source => TransformToRowItemDto(language, source, tvData, movieData))
                .Where(genreRow => genreRow != null);
        }

        return genres.Where(genre => genre.Items.Any()).ToList();
    }

    private static GenreRowItemDto? TransformToRowItemDto(
        string language,
        HomeSourceDto source,
        List<Tv> tvData,
        List<Movie> movieData)
    {
        switch (source.MediaType)
        {
            case TvMediaType:
            {
                Tv? tv = tvData.FirstOrDefault(tv => tv.Id == source.Id);

                return tv?.Id == null
                    ? null
                    : new GenreRowItemDto(tv,
                        language);
            }
            case MovieMediaType:
            {
                Movie? movie = movieData.FirstOrDefault(movie => movie.Id == source.Id);

                return movie?.Id == null
                    ? null
                    : new GenreRowItemDto(movie,
                        language);
            }
            default:
            {
                return null;
            }
        }
    }

    private async Task<List<Movie>> FetchMovieData(string language, IEnumerable<GenreRowDto<GenreRowItemDto>> genres)
    {
        List<int> movieIds = genres
            .SelectMany(s => s.Source
                .Where(s => s.MediaType == MovieMediaType)
                .Select(s => s.Id)).ToList();

        List<Movie> movieData = [];

        await foreach (Movie movie in _homeRepository.GetHomeMoviesQuery(_mediaContext, movieIds, language))
        {
            movieData.Add(movie);
        }

        return movieData;
    }

    private async Task<List<Tv>> FetchTvData(string language, IEnumerable<GenreRowDto<GenreRowItemDto>> genres)
    {
        List<int> tvIds = genres
            .SelectMany(genre => genre.Source
                .Where(source => source.MediaType == TvMediaType)
                .Select(source => source.Id)).ToList();

        List<Tv> tvData = [];

        await foreach (Tv tv in _homeRepository.GetHomeTvsQuery(_mediaContext, tvIds, language))
        {
            tvData.Add(tv);
        }

        return tvData;
    }

    private IEnumerable<GenreRowDto<GenreRowItemDto>> FetchGenres(List<Genre> genreItems)
    {
        return from genre in genreItems
            let name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name
            select new GenreRowDto<GenreRowItemDto>
            {
                Title = name,
                MoreLink = new($"/genre/{genre.Id}", UriKind.Relative),
                Id = genre.Id.ToString(),

                Source = genre.GenreMovies.Select(movie => new HomeSourceDto(movie.MovieId, MovieMediaType))
                    .Concat(genre.GenreTvShows.Select(tv => new HomeSourceDto(tv.TvId, TvMediaType)))
                    .Randomize()
                    .Take(MaximumCardsInCarousel)
            };
    }

    public async Task<Render> GetHomeData(Guid userId, string language, string country)
    {
        List<UserData> continueWatching = _homeRepository
            .GetContinueWatching(_mediaContext, userId, language, country)
            .ToList();

        List<NmCarouselDto<NmCardDto>> genres = [];

        List<int> movieIds = [];
        List<int> tvIds = [];

        HashSet<Genre> genreItems =
            await _homeRepository.GetHomeGenres(_mediaContext, userId, language, MaximumItemsPerPage);

        foreach (Genre genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies = genre
                .GenreMovies.Select(movie => new HomeSourceDto(movie.MovieId, MovieMediaType));

            IEnumerable<HomeSourceDto> tvs = genre
                .GenreTvShows.Select(tv => new HomeSourceDto(tv.TvId, TvMediaType));

            string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
            NmCarouselDto<NmCardDto> nmCarouselDto = new()
            {
                Title = name,
                MoreLink = new($"/genre/{genre.Id}", UriKind.Relative),
                Id = genre.Id.ToString(),
                Source = movies
                    .Concat(tvs)
                    .Randomize()
                    .Take(MaximumCardsInCarousel)
            };

            tvIds.AddRange(nmCarouselDto.Source
                .Where(source => source.MediaType == TvMediaType)
                .Select(source => source.Id));

            movieIds.AddRange(nmCarouselDto.Source
                .Where(source => source.MediaType == MovieMediaType)
                .Select(source => source.Id));

            genres.Add(nmCarouselDto);
        }

        List<Tv> tvData = [];
        await foreach (Tv tv in _homeRepository.GetHomeTvsQuery(_mediaContext, tvIds, language)) 
            tvData.Add(tv);

        List<Movie> movieData = [];
        await foreach (Movie movie in _homeRepository.GetHomeMoviesQuery(_mediaContext, movieIds, language))
            movieData.Add(movie);

        foreach (NmCarouselDto<NmCardDto> genre in genres)
            genre.Items = genre.Source
                .Select(source =>
                {
                    switch (source.MediaType)
                    {
                        case TvMediaType:
                        {
                            Tv? tv = tvData.FirstOrDefault(tv => tv.Id == source.Id);
                            return tv?.Id == null
                                ? null
                                : new NmCardDto(tv, language);
                        }
                        case MovieMediaType:
                        {
                            Movie? movie = movieData.FirstOrDefault(movie => movie.Id == source.Id);
                            return movie?.Id == null
                                ? null
                                : new NmCardDto(movie, language);
                        }
                        default:
                        {
                            return new();
                        }
                    }
                })
                .Where(genreRow => genreRow != null)
                .ToList() as List<NmCardDto>;

        genres = genres.Where(genre => genre.Items.Count != 0).ToList();

        NmCardDto? homeCardItem = EnsureNoEmptyCard(genres);

        List<Library> libraries = await _homeRepository.GetLibrariesQuery(_mediaContext, userId);
        List<NmCarouselDto<NmCardDto>> list = [];

        int animeCount = await _homeRepository.GetAnimeCountQuery(_mediaContext, userId);
        int movieCount = await _homeRepository.GetMovieCountQuery(_mediaContext, userId);
        int tvCount = await _homeRepository.GetTvCountQuery(_mediaContext, userId);

        foreach (Library library in libraries)
        {
            IEnumerable<Movie> movies =
                await _libraryRepository.GetLibraryMovies(userId, library.Id, language, 32, 0, m => m.CreatedAt,
                    "desc");
            IEnumerable<Tv> shows =
                await _libraryRepository.GetLibraryShows(userId, library.Id, language, 32, 0, m => m.CreatedAt, "desc");

            bool shouldPaginate = (library.Type == MovieMediaType && movieCount > MaximumItemsPerPage)
                                  || (library.Type == TvMediaType && tvCount > MaximumItemsPerPage)
                                  || (library.Type == AnimeMediaType && animeCount > MaximumItemsPerPage);

            NmCarouselDto<NmCardDto> item = new()
            {
                Id = library.Id.ToString(),
                Title = library.Title,
                MoreLink = shouldPaginate
                    ? new($"/libraries/{library.Id}/letter/A", UriKind.Relative)
                    : new($"/libraries/{library.Id}", UriKind.Relative),
                Items = movies.Select(movie => new NmCardDto(movie, country))
                    .Concat(shows.Select(tv => new NmCardDto(tv, country)))
                    .ToList()
            };

            if (item.Items.Count != 0)
                list.Add(item);
        }

        return new()
        {
            Data =
            [
                ..homeCardItem != null
                    ? new[]
                    {
                        new ComponentBuilder<NmCardDto>()
                            .WithComponent("NMHomeCard")
                            .WithUpdate("pageLoad", "/home/card")
                            .WithProps((props, id) => props.WithData(homeCardItem))
                            .Build()
                    }
                    : [],

                new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithProps((props, id) => props
                        .WithId("continue")
                        .WithNextId($"library_{list.ElementAtOrDefault(0)?.Id}")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country, id)))
                    .Build(),

                ..list.Select((genre, index) => new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithProps((props, id) => props
                        .WithId($"library_{genre.Id}")
                        .WithPreviousId(index == 0
                            ? "continue"
                            : $"library_{list.ElementAtOrDefault(index - 1)?.Id}")
                        .WithNextId(index == list.Count - 1
                            ? $"library_{genres.FirstOrDefault()?.Id}"
                            : $"library_{list.ElementAtOrDefault(index + 1)?.Id}")
                        .WithTitle("Latest in " + genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<NmCardDto>()
                                    .WithComponent("NMCard")
                                    .WithProps((p, _) => p
                                        .WithData(item)
                                        .WithWatch())
                                    .Build())))
                    .Build()),

                ..genres.Select((genre, index) => new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithProps((props, id) => props
                        .WithId($"library_{genre.Id}")
                        .WithPreviousId(index == 0
                            ? "continue"
                            : $"library_{list.ElementAtOrDefault(index - 1)?.Id}")
                        .WithNextId(index == genres.Count - 1
                            ? $"library_{list.FirstOrDefault()?.Id}"
                            : $"library_{genres.ElementAtOrDefault(index + 1)?.Id}")
                        .WithTitle(genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<NmCardDto?>()
                                    .WithComponent("NMCard")
                                    .WithProps((p, _) => p
                                        .WithData(item)
                                        .WithWatch())
                                    .Build())))
                    .Build())
            ]
        };
    }

    private static NmCardDto? EnsureNoEmptyCard(List<NmCarouselDto<NmCardDto>> genres)
    {
        return genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault();
    }

    public async Task<Render> GetHomeCard(Guid userId, string language, Ulid replaceId)
    {
        Tv? tv = await _libraryRepository.GetRandomTvShow(userId, language);

        Movie? movie = await _libraryRepository.GetRandomMovie(userId, language);

        List<NmCardDto> genres = [];
        if (tv != null)
            genres.Add(new(tv, language));

        if (movie != null)
            genres.Add(new(movie, language));

        NmCardDto? homeCardItem = genres
            .Where(g => !string.IsNullOrWhiteSpace(g.Title))
            .Randomize()
            .FirstOrDefault();

        return new()
        {
            Data =
            [
                new ComponentBuilder<NmCardDto?>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithReplacing(replaceId)
                    .WithProps((props, _) => props.WithData(homeCardItem))
                    .Build()
            ]
        };
    }

    public async Task<ScreensaverDto> GetScreensaverContent(Guid userId)
    {
        HashSet<Image> data = await _homeRepository.GetScreensaverImagesQuery(_mediaContext, userId);

        IEnumerable<Image> logos = data.Where(image => image.Type == "logo");

        IEnumerable<ScreensaverDataDto> tvCollection = data
            .Where(image => image is { TvId: not null, Type: "backdrop" })
            .DistinctBy(image => image.TvId)
            .Select(image => new ScreensaverDataDto(image, logos, TvMediaType));

        IEnumerable<ScreensaverDataDto> movieCollection = data
            .Where(image => image is { MovieId: not null, Type: "backdrop" })
            .DistinctBy(image => image.MovieId)
            .Select(image => new ScreensaverDataDto(image, logos, MovieMediaType));

        return new()
        {
            Data = tvCollection
                .Concat(movieCollection)
                .Where(image => image.Meta?.Logo != null)
                .Randomize()
        };
    }

    public async Task<Render> GetHomeTvContent(Guid userId, string language, string country)
    {
        IEnumerable<UserData> continueWatching = _homeRepository
            .GetContinueWatching(_mediaContext, userId, language, country)
            .Where(item => item.Tv?.Episodes.Last().VideoFiles.First().Id != item.VideoFileId ||
                           item.Time < item.VideoFile.Duration.ToSeconds() * 0.8);

        List<NmCarouselDto<NmCardDto?>> genres = [];

        HashSet<Genre> genreItems =
            await _homeRepository.GetHomeGenres(_mediaContext, userId, language, MaximumItemsPerPage);
        List<int> movieIds = [];
        List<int> tvIds = [];

        foreach (Genre genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies =
                genre.GenreMovies.Select(movie => new HomeSourceDto(movie.MovieId, MovieMediaType));
            IEnumerable<HomeSourceDto> tvs = genre.GenreTvShows.Select(tv => new HomeSourceDto(tv.TvId, TvMediaType));

            string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
            NmCarouselDto<NmCardDto?> nmCarouselDto = new()
            {
                Title = name,
                MoreLink = new($"/genre/{genre.Id}", UriKind.Relative),
                Id = genre.Id.ToString(),
                Source = movies.Concat(tvs).Randomize().Take(28)
            };

            tvIds.AddRange(nmCarouselDto.Source
                .Where(s => s.MediaType == TvMediaType)
                .Select(s => s.Id));
            
            movieIds.AddRange(nmCarouselDto.Source
                .Where(s => s.MediaType == MovieMediaType)
                .Select(s => s.Id));

            genres.Add(nmCarouselDto);
        }

        List<Tv> tvData = [];
        List<Movie> movieData = [];

        await foreach (Tv tv in _homeRepository.GetHomeTvsQuery(_mediaContext, tvIds, language))
            tvData.Add(tv);

        await foreach (Movie movie in _homeRepository.GetHomeMoviesQuery(_mediaContext, movieIds, language))
            movieData.Add(movie);

        // Process genres and items
        foreach (NmCarouselDto<NmCardDto?> genre in genres)
            genre.Items = genre.Source
                .Select(source => source.MediaType switch
                {
                    TvMediaType => tvData.FirstOrDefault(t => t.Id == source.Id)?.Id == null
                        ? null
                        : new NmCardDto(tvData.First(t => t.Id == source.Id), country),
                    MovieMediaType => movieData.FirstOrDefault(m => m.Id == source.Id)?.Id == null
                        ? null
                        : new NmCardDto(movieData.First(m => m.Id == source.Id), country),
                    _ => null
                })
                .Where(item => item != null)
                .ToList();

        genres = genres.Where(genre => genre.Items.Count != 0).ToList();
        
        NmCardDto? homeCardItem = genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()?.Items.Randomize().FirstOrDefault();

        IEnumerable<Library> libraries = await _libraryRepository.GetLibraries(userId);
        List<NmCarouselDto<NmCardDto>> list = [];

        foreach (Library library in libraries)
        {
            IEnumerable<Movie> movies =
                await _libraryRepository.GetLibraryMovies(userId, library.Id, language, 32, 0, m => m.CreatedAt,
                    "desc");
            IEnumerable<Tv> shows =
                await _libraryRepository.GetLibraryShows(userId, library.Id, language, 32, 0, m => m.CreatedAt, "desc");

            list.Add(new()
            {
                Title = library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                Items = movies.Select(movie => new NmCardDto(movie, country))
                    .Concat(shows.Select(tv => new NmCardDto(tv, country)))
                    .ToList()
            });
        }

        return new()
        {
            Data =
            [
                new ComponentBuilder<NmCardDto?>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps((props, id) => props.WithData(homeCardItem))
                    .Build(),

                new ComponentBuilder<NmCarouselDto<NmCardDto>>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithProps((props, id) => props
                        .WithNextId(genres.FirstOrDefault()?.Id ?? "continue")
                        .WithPreviousId("continue")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country, id)))
                    .Build(),

                ..list.Select((genre, index) => new ComponentBuilder<NmCarouselDto<NmCardDto>>()
                    .WithComponent("NMCarousel")
                    .WithProps((props, id) => props
                        .WithId(genre.Id)
                        .WithPreviousId(index == 0
                            ? "continue"
                            : $"library_{list.ElementAtOrDefault(index - 1)?.Id}")
                        .WithNextId(index == list.Count - 1
                            ? genres.FirstOrDefault()?.Id ?? "continue"
                            : $"library_{list.ElementAtOrDefault(index + 1)?.Id}")
                        .WithTitle("Latest in " + genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<NmCardDto>()
                                    .WithComponent("NMCard")
                                    .WithProps((p, _) => p
                                        .WithData(item)
                                        .WithWatch())
                                    .Build())))
                    .Build()),

                ..genres.Select((genre, index) => new ComponentBuilder<NmCarouselDto<NmCardDto>>()
                    .WithComponent("NMCarousel")
                    .WithProps((props, id) => props
                        .WithId(genre.Id)
                        .WithPreviousId(index == 0
                            ? $"library_{list.LastOrDefault()?.Id ?? "continue"}"
                            : genres.ElementAtOrDefault(index - 1)?.Id ?? "continue")
                        .WithNextId(index == genres.Count - 1
                            ? "continue"
                            : genres.ElementAtOrDefault(index + 1)?.Id ?? "continue")
                        .WithTitle(genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<NmCardDto>()
                                    .WithComponent("NMCard")
                                    .WithProps((p, _) => p
                                        .WithData(item ?? new())
                                        .WithWatch())
                                    .Build())))
                    .Build())
            ]
        };
    }

    public async Task<Render> GetHomeContinueContent(Guid userId, string language, string country, Ulid replaceId)
    {
        IEnumerable<UserData> continueWatching = _homeRepository
            .GetContinueWatching(_mediaContext, userId, language, country)
            .Where(item => item.Tv?.Episodes.Last().VideoFiles.First().Id != item.VideoFileId ||
                           item.Time < item.VideoFile.Duration.ToSeconds() * 0.8);

        return new()
        {
            Data =
            [
                new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithProps((props, id) => props
                        .WithNextId(28)
                        .WithPreviousId("continue")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country, id)))
                    .Build()
            ]
        };
    }

    private static IEnumerable<ComponentDto<NmCardDto>> GetContinueWatchingItems(IEnumerable<UserData> continueWatching,
        string country, Ulid id)
    {
        return continueWatching
            .Select(item => new ComponentDto<NmCardDto>
            {
                Component = "NMCard",
                Props =
                {
                    Data = new(item, country),
                    Watch = true,
                    ContextMenuItems =
                    [
                        new()
                        {
                            { "label", "Remove from watchlist".Localize() },
                            { "icon", "mooooom-trash" },
                            { "method", "DELETE" },
                            { "confirm", "Are you sure you want to remove this from continue watching?".Localize() },
                            {
                                "args", new Dictionary<string, object>
                                {
                                    { "url", new Uri("/userdata/continue", UriKind.Relative) },
                                    { "replaceKey", id }
                                }
                            }
                        }
                    ]
                }
            })
            .DistinctBy(item => item.Props.Data?.Link);
    }
}