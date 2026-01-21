using NoMercy.Api.Controllers.V1.Media;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.NmSystem;
using NoMercy.Data.Repositories;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;

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

    public async Task<List<GenreRowDto<GenreRowItemDto>>> GetHomePageContent(
        Guid userId,
        string language,
        string country,
        PageRequestDto request)
    {
        List<Genre> genreItems = await HomeResponseDto
            .GetHome(_mediaContext, userId, language, request.Take, request.Page);

        List<GenreRowDto<GenreRowItemDto>> genres = FetchGenres(genreItems).ToList();
        List<Tv> tvData = await FetchTvData(language, country, genres);
        List<Movie> movieData = await FetchMovieData(language, country, genres);

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
        return source.MediaType switch
        {
            Config.TvMediaType => tvData.FirstOrDefault(t => t.Id == source.Id) is { } tv
                ? new GenreRowItemDto(tv, language)
                : null,
            Config.MovieMediaType => movieData.FirstOrDefault(m => m.Id == source.Id) is { } movie
                ? new GenreRowItemDto(movie, language)
                : null,
            _ => null
        };
    }

    private async Task<List<Movie>> FetchMovieData(string language, string country, IEnumerable<GenreRowDto<GenreRowItemDto>> genres)
    {
        List<int> movieIds = genres
            .SelectMany(genreRow => genreRow.Source
                .Where(homeSource => homeSource.MediaType == Config.MovieMediaType)
                .Select(h => h.Id)).ToList();

        return await _homeRepository.GetHomeMovies(_mediaContext, movieIds, language, country);
    }

    private async Task<List<Tv>> FetchTvData(string language, string country, IEnumerable<GenreRowDto<GenreRowItemDto>> genres)
    {
        List<int> tvIds = genres
            .SelectMany(genre => genre.Source
                .Where(source => source.MediaType == Config.TvMediaType)
                .Select(source => source.Id)).ToList();

        return await _homeRepository.GetHomeTvs(_mediaContext, tvIds, language, country);
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
                Source = genre.GenreMovies.Select(movie => new HomeSourceDto(movie.MovieId, Config.MovieMediaType))
                    .Concat(genre.GenreTvShows.Select(tv => new HomeSourceDto(tv.TvId, Config.TvMediaType)))
                    .Randomize()
                    .Take(Config.MaximumCardsInCarousel)
            };
    }

    public async Task<ComponentResponse> GetHomeData(Guid userId, string language, string country)
    {
        // Run initial independent queries in parallel
        Task<HashSet<UserData>> continueWatchingTask = _homeRepository
            .GetContinueWatchingAsync(_mediaContext, userId, language, country);
        Task<List<Genre>> genreItemsTask = _homeRepository
            .GetHomeGenresAsync(_mediaContext, userId, language, Config.MaximumItemsPerPage);
        Task<List<Library>> librariesTask = _homeRepository
            .GetLibrariesAsync(_mediaContext, userId);
        Task<int> animeCountTask = _homeRepository.GetAnimeCountAsync(_mediaContext, userId);
        Task<int> movieCountTask = _homeRepository.GetMovieCountAsync(_mediaContext, userId);
        Task<int> tvCountTask = _homeRepository.GetTvCountAsync(_mediaContext, userId);

        await Task.WhenAll(continueWatchingTask, genreItemsTask, librariesTask, animeCountTask, movieCountTask, tvCountTask);

        HashSet<UserData> continueWatching = continueWatchingTask.Result;
        List<Genre> genreItems = genreItemsTask.Result;
        List<Library> libraries = librariesTask.Result;
        int animeCount = animeCountTask.Result;
        int movieCount = movieCountTask.Result;
        int tvCount = tvCountTask.Result;

        // Collect genre source data
        List<GenreSourceData> genreSourceList = [];
        List<int> movieIds = [];
        List<int> tvIds = [];

        foreach (Genre genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies = genre.GenreMovies
                .Select(movie => new HomeSourceDto(movie.MovieId, Config.MovieMediaType));
            IEnumerable<HomeSourceDto> tvs = genre.GenreTvShows
                .Select(tv => new HomeSourceDto(tv.TvId, Config.TvMediaType));

            string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
            List<HomeSourceDto> source = movies.Concat(tvs).Randomize().Take(Config.MaximumCardsInCarousel).ToList();

            tvIds.AddRange(source.Where(s => s.MediaType == Config.TvMediaType).Select(s => s.Id));
            movieIds.AddRange(source.Where(s => s.MediaType == Config.MovieMediaType).Select(s => s.Id));

            genreSourceList.Add(new(genre.Id.ToString(), name,
                new($"/genre/{genre.Id}", UriKind.Relative), source));
        }

        // Fetch media data in parallel
        Task<List<Tv>> tvDataTask = _homeRepository.GetHomeTvs(_mediaContext, tvIds, language, country);
        Task<List<Movie>> movieDataTask = _homeRepository.GetHomeMovies(_mediaContext, movieIds, language, country);

        await Task.WhenAll(tvDataTask, movieDataTask);

        List<Tv> tvData = tvDataTask.Result;
        List<Movie> movieData = movieDataTask.Result;

        // Build genre carousels with resolved items
        List<GenreCarouselData> genreCarousels = genreSourceList
            .Select(g => new GenreCarouselData(
                g.Id,
                g.Title,
                g.MoreLink,
                g.Source.Select(source => ResolveCardData(source, tvData, movieData, country))
                    .Where(c => c != null)
                    .Cast<CardData>()
                    .ToList()
            ))
            .Where(g => g.Items.Count > 0)
            .ToList();

        // Get random home card
        CardData? homeCardItem = genreCarousels
            .Where(g => !string.IsNullOrEmpty(g.Title))
            .SelectMany(g => g.Items)
            .Where(c => !string.IsNullOrWhiteSpace(c.Title))
            .Randomize()
            .FirstOrDefault();

        // Build library carousels - fetch all library data in parallel - each task needs its own MediaContext for thread safety
        List<GenreCarouselData> libraryCarousels = [];

        List<Task<(Library library, List<Movie> movies, List<Tv> shows)>> libraryTasks = libraries
            .Select(async library =>
            {
                MediaContext context = new();
                List<Movie> libraryMovies = [];
                await foreach (Movie movie in _libraryRepository
                    .GetLibraryMovies(context, userId, library.Id, language, 6, 0, m => m.CreatedAt, "desc"))
                {
                    libraryMovies.Add(movie);
                }

                List<Tv> libraryShows = [];
                await foreach (Tv tv in _libraryRepository
                    .GetLibraryShows(context, userId, library.Id, language, 6, 0, m => m.CreatedAt, "desc"))
                {
                    libraryShows.Add(tv);
                }

                return (library, libraryMovies, libraryShows);
            })
            .ToList();

        (Library library, List<Movie> movies, List<Tv> shows)[] libraryResults = await Task.WhenAll(libraryTasks);

        foreach ((Library library, List<Movie> libraryMovies, List<Tv> libraryShows) in libraryResults)
        {
            bool shouldPaginate = (library.Type == Config.MovieMediaType && movieCount > Config.MaximumItemsPerPage)
                                  || (library.Type == Config.TvMediaType && tvCount > Config.MaximumItemsPerPage)
                                  || (library.Type == Config.AnimeMediaType && animeCount > Config.MaximumItemsPerPage);

            List<CardData> items = libraryMovies.Select(m => new CardData(m, country))
                .Concat(libraryShows.Select(t => new CardData(t, country)))
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            if (items.Count > 0)
            {
                Uri moreLink = shouldPaginate
                    ? new($"/libraries/{library.Id}/letter/A", UriKind.Relative)
                    : new Uri($"/libraries/{library.Id}", UriKind.Relative);

                libraryCarousels.Add(new(library.Id.ToString(), library.Title, moreLink, items));
            }
        }

        // Build components
        List<ComponentEnvelope> components = [];

        // Home card
        if (homeCardItem != null)
        {
            components.Add(
                Component.HomeCard(new()
                    {
                        Id = homeCardItem.Id,
                        Title = homeCardItem.Title,
                        Overview = homeCardItem.Overview,
                        Backdrop = homeCardItem.Backdrop,
                        Poster = homeCardItem.Poster,
                        Logo = homeCardItem.Logo,
                        Year = homeCardItem.Year,
                        ColorPalette = homeCardItem.ColorPalette,
                        Link = homeCardItem.Link,
                        MediaType = homeCardItem.Type
                    })
                    .WithUpdate("pageLoad", "/home/card")
                    .Build()
            );
        }

        // Continue watching carousel
        components.Add(
            Component.Carousel()
                .WithId("continue")
                .WithNavigation(null, libraryCarousels.Count > 0 ? $"library_{libraryCarousels[0].Id}" : null)
                .WithTitle("Continue watching".Localize())
                .WithUpdate("pageLoad", "/home/continue")
                .WithItems(BuildContinueWatchingCards(continueWatching, country))
                .Build()
        );

        // Library carousels
        for (int i = 0; i < libraryCarousels.Count; i++)
        {
            GenreCarouselData lib = libraryCarousels[i];

            string prevId = i == 0 ? "continue" : $"library_{libraryCarousels[i - 1].Id}";
            string? nextId = i == libraryCarousels.Count - 1
                ? genreCarousels.Count > 0 ? $"genre_{genreCarousels[0].Id}" : null
                : $"library_{libraryCarousels[i + 1].Id}";

            components.Add(
                Component.Carousel()
                    .WithId($"library_{lib.Id}")
                    .WithNavigation(prevId, nextId)
                    .WithTitle($"Latest in {lib.Title}")
                    .WithMoreLink(lib.MoreLink)
                    .WithItems(lib.Items.Select(item =>
                        Component.Card(item).WithWatch().Build()))
                    .Build()
            );
        }

        // Genre carousels
        for (int i = 0; i < genreCarousels.Count; i++)
        {
            GenreCarouselData genre = genreCarousels[i];

            string prevId = i == 0
                ? libraryCarousels.Count > 0 ? $"library_{libraryCarousels[^1].Id}" : "continue"
                : $"genre_{genreCarousels[i - 1].Id}";
            string nextId = i == genreCarousels.Count - 1
                ? libraryCarousels.Count > 0 ? $"library_{libraryCarousels[0].Id}" : "continue"
                : $"genre_{genreCarousels[i + 1].Id}";

            components.Add(
                Component.Carousel()
                    .WithId($"genre_{genre.Id}")
                    .WithNavigation(prevId, nextId)
                    .WithTitle(genre.Title)
                    .WithMoreLink(genre.MoreLink)
                    .WithItems(genre.Items.Select(item =>
                        Component.Card(item).WithWatch().Build()))
                    .Build()
            );
        }

        return new() { Data = components };
    }

    private static CardData? ResolveCardData(HomeSourceDto source, List<Tv> tvData, List<Movie> movieData, string country, bool watch = false)
    {
        return source.MediaType switch
        {
            Config.TvMediaType => tvData.FirstOrDefault(t => t.Id == source.Id) is { } tv
                ? new CardData(tv, country, watch)
                : null,
            Config.MovieMediaType => movieData.FirstOrDefault(m => m.Id == source.Id) is { } movie
                ? new CardData(movie, country, watch)
                : null,
            _ => null
        };
    }

    private static IEnumerable<ComponentEnvelope> BuildContinueWatchingCards(IEnumerable<UserData> continueWatching, string country)
    {
        return continueWatching
            .Select(item => Component.Card(new(item, country))
                .WithWatch()
                .WithContextMenu([
                    new()
                    {
                        Title = "Remove from watchlist".Localize(),
                        Icon = "mooooom-trash",
                        Method = "DELETE",
                        Confirm = "Are you sure you want to remove this from continue watching?".Localize(),
                        Args = new()
                        {
                            { "url", new Uri("/userdata/continue", UriKind.Relative) }
                        }
                    }
                ])
                .Build())
            .DistinctBy(c => ((LeafProps<CardData>)c.Props).Data?.Link);
    }

    public async Task<ComponentResponse> GetHomeCard(Guid userId, string language, Ulid replaceId)
    {
        Tv? tv = await _libraryRepository.GetRandomTvShow(userId, language);
        Movie? movie = await _libraryRepository.GetRandomMovie(userId, language);

        List<CardData> candidates = [];
        if (tv != null)
            candidates.Add(new(tv, language));
        if (movie != null)
            candidates.Add(new(movie, language));

        CardData? homeCardItem = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Title))
            .Randomize()
            .FirstOrDefault();

        return new()
        {
            Data =
            [
                Component.HomeCard(homeCardItem != null
                        ? new()
                        {
                            Id = homeCardItem.Id,
                            Title = homeCardItem.Title,
                            Overview = homeCardItem.Overview,
                            Backdrop = homeCardItem.Backdrop,
                            Poster = homeCardItem.Poster,
                            Logo = homeCardItem.Logo,
                            Year = homeCardItem.Year,
                            ColorPalette = homeCardItem.ColorPalette,
                            Link = homeCardItem.Link,
                            MediaType = homeCardItem.Type
                        }
                        : new HomeCardData())
                    .WithUpdate("pageLoad", "/home/card")
                    .WithReplacing(replaceId)
                    .Build()
            ]
        };
    }

    public async Task<ScreensaverDto> GetSetupScreensaverContent(Guid userId)
    {
        HashSet<Image> data = await _homeRepository.GetScreensaverImagesAsync(_mediaContext, userId);

        IEnumerable<Image> logos = data.Where(image => image.Type == "logo");

        IEnumerable<ScreensaverDataDto> tvCollection = data
            .Where(image => image is { TvId: not null, Type: "backdrop" })
            .DistinctBy(image => image.TvId)
            .Select(image => new ScreensaverDataDto(image, logos, Config.TvMediaType));

        IEnumerable<ScreensaverDataDto> movieCollection = data
            .Where(image => image is { MovieId: not null, Type: "backdrop" })
            .DistinctBy(image => image.MovieId)
            .Select(image => new ScreensaverDataDto(image, logos, Config.MovieMediaType));

        return new()
        {
            Data = tvCollection
                .Concat(movieCollection)
                .Where(image => image.Meta?.Logo != null)
                .Randomize()
        };
    }

    public async Task<ComponentResponse> GetHomeTvContent(Guid userId, string language, string country)
    {
        HashSet<UserData> continueWatching = await _homeRepository
            .GetContinueWatchingAsync(_mediaContext, userId, language, country);

        // Collect genre source data
        List<GenreSourceData> genreSourceList = [];
        List<int> movieIds = [];
        List<int> tvIds = [];

        List<Genre> genreItems = await _homeRepository.GetHomeGenresAsync(_mediaContext, userId, language, Config.MaximumItemsPerPage);

        foreach (Genre genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies = genre.GenreMovies
                .Select(movie => new HomeSourceDto(movie.MovieId, Config.MovieMediaType));
            IEnumerable<HomeSourceDto> tvs = genre.GenreTvShows
                .Select(tv => new HomeSourceDto(tv.TvId, Config.TvMediaType));

            string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
            List<HomeSourceDto> source = movies.Concat(tvs).Randomize().Take(Config.MaximumCardsInCarousel).ToList();

            tvIds.AddRange(source.Where(s => s.MediaType == Config.TvMediaType).Select(s => s.Id));
            movieIds.AddRange(source.Where(s => s.MediaType == Config.MovieMediaType).Select(s => s.Id));

            genreSourceList.Add(new(genre.Id.ToString(), name,
                new($"/genre/{genre.Id}", UriKind.Relative), source));
        }

        // Fetch data
        List<Tv> tvData = await _homeRepository.GetHomeTvs(_mediaContext, tvIds, language, country);
        List<Movie> movieData = await _homeRepository.GetHomeMovies(_mediaContext, movieIds, language, country);

        // Build genre carousels
        List<GenreCarouselData> genreCarousels = genreSourceList
            .Select(g => new GenreCarouselData(
                g.Id,
                g.Title,
                g.MoreLink,
                g.Source.Select(source => ResolveCardData(source, tvData, movieData, country, watch: true))
                    .Where(c => c != null)
                    .Cast<CardData>()
                    .ToList()
            ))
            .Where(g => g.Items.Count > 0)
            .ToList();


        // Build components
        List<ComponentEnvelope> components = [];

        // Continue watching
        components.Add(
            Component.Carousel()
                .WithId("continue")
                .WithTitle("Continue watching".Localize())
                .WithUpdate("pageLoad", "/home/continue")
                .WithItems(BuildContinueWatchingCards(continueWatching, country))
                .Build()
        );

        // Genre carousels (limited to 6 items for TV)
        foreach (GenreCarouselData genre in genreCarousels)
        {
            components.Add(
                Component.Carousel()
                    .WithId($"genre_{genre.Id}")
                    .WithTitle(genre.Title)
                    .WithMoreLink(genre.MoreLink)
                    .WithItems(genre.Items
                        .Take(6)
                        .Select(item => Component
                            .Card(item)
                            .WithWatch()
                            .Build()
                        ))
                    .Build()
            );
        }

        return new() { Data = components };
    }

    public async Task<ComponentResponse> GetHomeContinueContent(Guid userId, string language, string country, Ulid replaceId)
    {
        HashSet<UserData> continueWatching = await _homeRepository
            .GetContinueWatchingAsync(_mediaContext, userId, language, country);

        IEnumerable<UserData> filtered = continueWatching
            .Where(item => item.Tv?.Episodes.LastOrDefault()?.VideoFiles.FirstOrDefault()?.Id != item.VideoFileId ||
                           item.Time < (item.VideoFile.Duration?.ToSeconds() ?? 0) * 0.8);

        return new()
        {
            Data =
            [
                Component.Carousel()
                    .WithId("continue")
                    .WithNavigation("continue", 28)
                    .WithTitle("Continue watching".Localize())
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithItems(BuildContinueWatchingCards(filtered, country))
                    .WithReplacing(replaceId)
                    .Build()
            ]
        };
    }

    // Helper records for intermediate data
    private record GenreSourceData(string Id, string Title, Uri MoreLink, List<HomeSourceDto> Source);
    private record GenreCarouselData(string Id, string Title, Uri MoreLink, List<CardData> Items);
}
