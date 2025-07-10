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
    private readonly MediaContext _mediaContext;
    private readonly LibraryRepository _libraryRepository;
    private readonly HomeRepository _homeRepository;

    public HomeService(HomeRepository homeRepository, LibraryRepository libraryRepository, MediaContext mediaContext)
    {
        _homeRepository = homeRepository;
        _libraryRepository = libraryRepository;
        _mediaContext = mediaContext;
    }

    public async Task<Render> GetHomeData(Guid userId, string language, string country)
    {
        List<UserData> continueWatching = _homeRepository
            .GetContinueWatching(_mediaContext, userId, language, country)
            .ToList();

        List<NmCarouselDto<NmCardDto>> genres = [];

        List<int> movieIds = [];
        List<int> tvIds = [];

        HashSet<Genre> genreItems = await _homeRepository.GetHomeGenres(_mediaContext, userId, language, 300);

        foreach (Genre genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies = genre
                .GenreMovies.Select(movie => new HomeSourceDto(movie.MovieId, "movie"));

            IEnumerable<HomeSourceDto> tvs = genre
                .GenreTvShows.Select(tv => new HomeSourceDto(tv.TvId, "tv"));

            string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
            NmCarouselDto<NmCardDto> nmCarouselDto = new()
            {
                Title = name,
                MoreLink = new($"/genre/{genre.Id}", UriKind.Relative),
                Id = genre.Id.ToString(),
                Source = movies
                    .Concat(tvs)
                    .Randomize()
                    .Take(28)
            };

            tvIds.AddRange(nmCarouselDto.Source
                .Where(source => source.MediaType == "tv")
                .Select(source => source.Id));

            movieIds.AddRange(nmCarouselDto.Source
                .Where(source => source.MediaType == "movie")
                .Select(source => source.Id));

            genres.Add(nmCarouselDto);
        }

        List<Tv> tvData = [];
        await foreach (Tv tv in _homeRepository.GetHomeTvsQuery(_mediaContext, tvIds, language)) tvData.Add(tv);

        List<Movie> movieData = [];
        await foreach (Movie movie in _homeRepository.GetHomeMoviesQuery(_mediaContext, movieIds, language))
            movieData.Add(movie);

        foreach (NmCarouselDto<NmCardDto> genre in genres)
            genre.Items = genre.Source
                .Select(source =>
                {
                    switch (source.MediaType)
                    {
                        case "tv":
                        {
                            Tv? tv = tvData.FirstOrDefault(tv => tv.Id == source.Id);
                            return tv?.Id == null
                                ? null
                                : new NmCardDto(tv, language);
                        }
                        case "movie":
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

        genres = genres.Where(genre => genre.Items.Any()).ToList();

        NmCardDto? homeCardItem = genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault();

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

            bool shouldPaginate = (library.Type == "movie" && movieCount > 300)
                                  || (library.Type == "tv" && tvCount > 300)
                                  || (library.Type == "anime" && animeCount > 300);

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

            if (item.Items.Any())
                list.Add(item);
        }

        return new()
        {
            Data =
            [
                ..homeCardItem != null ? new[] {
                    new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps(props => props.WithData(homeCardItem))
                    .Build()
                } : [],
                
                ..continueWatching.Count > 0 ? new[] {
                new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithProps(props => props
                        .WithId("continue")
                        .WithNextId($"library_{list.ElementAtOrDefault(0)?.Id}")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country)))
                    .Build(),
                } : [],

                ..list.Select((genre, index) => new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId($"library_{genre.Id}")
                        .WithPreviousId(index == 0
                            ? "continue"
                            : $"library_{list.ElementAtOrDefault(index - 1)?.Id}")
                        .WithNextId(index == list.Count - 1
                            ? genres.FirstOrDefault()?.Id
                            : $"library_{list.ElementAtOrDefault(index + 1)?.Id}")
                        .WithTitle("Latest in " + genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<NmCardDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item)
                                        .WithWatch())
                                    .Build())))
                    .Build()),

                ..genres.Select((genre, index) => new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId($"library_{genre.Id}")
                        .WithPreviousId(index == 0
                            ? "continue"
                            : $"library_{list.ElementAtOrDefault(index - 1)?.Id}")
                        .WithNextId(index == list.Count - 1
                            ? genres.FirstOrDefault()?.Id
                            : $"library_{list.ElementAtOrDefault(index + 1)?.Id}")
                        .WithTitle("Latest in " + genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<NmCardDto?>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item)
                                        .WithWatch())
                                    .Build())))
                    .Build())
            ]
        };
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
                    .WithProps(props => props.WithData(homeCardItem))
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
            .Select(image => new ScreensaverDataDto(image, logos, "tv"));

        IEnumerable<ScreensaverDataDto> movieCollection = data
            .Where(image => image is { MovieId: not null, Type: "backdrop" })
            .DistinctBy(image => image.MovieId)
            .Select(image => new ScreensaverDataDto(image, logos, "movie"));

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

        HashSet<Genre> genreItems = await _homeRepository.GetHomeGenres(_mediaContext, userId, language, 300);
        List<int> movieIds = [];
        List<int> tvIds = [];

        foreach (Genre genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies =
                genre.GenreMovies.Select(movie => new HomeSourceDto(movie.MovieId, "movie"));
            IEnumerable<HomeSourceDto> tvs = genre.GenreTvShows.Select(tv => new HomeSourceDto(tv.TvId, "tv"));

            string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
            NmCarouselDto<NmCardDto?> nmCarouselDto = new()
            {
                Title = name,
                MoreLink = new($"/genre/{genre.Id}", UriKind.Relative),
                Id = genre.Id.ToString(),
                Source = movies.Concat(tvs).Randomize().Take(28)
            };

            tvIds.AddRange(nmCarouselDto.Source.Where(s => s.MediaType == "tv").Select(s => s.Id));
            movieIds.AddRange(nmCarouselDto.Source.Where(s => s.MediaType == "movie").Select(s => s.Id));

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
                    "tv" => tvData.FirstOrDefault(t => t.Id == source.Id)?.Id == null
                        ? null
                        : new NmCardDto(tvData.First(t => t.Id == source.Id), country),
                    "movie" => movieData.FirstOrDefault(m => m.Id == source.Id)?.Id == null
                        ? null
                        : new NmCardDto(movieData.First(m => m.Id == source.Id), country),
                    _ => null
                })
                .Where(item => item != null)
                .ToList();

        genres = genres.Where(genre => genre.Items.Any()).ToList();
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
                    .WithProps(props => props.WithData(homeCardItem))
                    .Build(),

                new ComponentBuilder<NmCarouselDto<NmCardDto>>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithProps(props => props
                        .WithNextId(genres.FirstOrDefault()?.Id ?? "continue")
                        .WithPreviousId("continue")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country)))
                    .Build(),

                ..list.Select((genre, index) => new ComponentBuilder<NmCarouselDto<NmCardDto>>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
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
                                    .WithProps(cardProps => cardProps
                                        .WithData(item)
                                        .WithWatch())
                                    .Build())))
                    .Build()),

                ..genres.Select((genre, index) => new ComponentBuilder<NmCarouselDto<NmCardDto>>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
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
                                    .WithProps(cardProps => cardProps
                                        .WithData(item)
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
                    .WithProps(props => props
                        .WithNextId(28)
                        .WithPreviousId("continue")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country)))
                    .Build()
            ]
        };
    }

    private static IEnumerable<ComponentDto<NmCardDto>> GetContinueWatchingItems(
        IEnumerable<UserData> continueWatching, string country)
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
                                    { "url", new Uri($"/userdata/continue", UriKind.Relative) }
                                }
                            }
                        }
                    ]
                }
            })
            .DistinctBy(item => item.Props.Data?.Link);
    }
}