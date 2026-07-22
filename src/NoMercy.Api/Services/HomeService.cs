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

using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Services;

public class HomeService(IHomeRepository homeRepository, ILibraryRepository libraryRepository)
{
    public async Task<List<GenreRowDto<GenreRowItemDto>>> GetHomePageContent(
        Guid userId,
        string language,
        string country,
        PageRequestDto request
    )
    {
        List<Genre> genreItems = await homeRepository.GetHome(
            userId: userId,
            language: language,
            take: request.Take,
            page: request.Page
        );

        List<GenreRowDto<GenreRowItemDto>> genres = FetchGenres(genreItems: genreItems).ToList();

        List<int> movieIds = genres
            .SelectMany(selector: genreRow =>
                genreRow
                    .Source.Where(predicate: homeSource => homeSource.MediaType == MediaTypes.MovieMediaType)
                    .Select(selector: h => h.Id)
            )
            .ToList();

        List<int> tvIds = genres
            .SelectMany(selector: genre =>
                genre
                    .Source.Where(predicate: source => source.MediaType == MediaTypes.TvMediaType)
                    .Select(selector: source => source.Id)
            )
            .ToList();

        HomeTvsAndMoviesData tvsAndMovies = await homeRepository.GetHomeTvsAndMoviesAsync(
            tvIds: tvIds,
            movieIds: movieIds,
            language: language,
            country: country
        );

        foreach (GenreRowDto<GenreRowItemDto> genre in genres)
        {
            genre.Items = genre
                .Source.Select(selector: source =>
                    TransformToRowItemDto(
                        country: country,
                        source: source,
                        tvData: tvsAndMovies.TvData,
                        movieData: tvsAndMovies.MovieData
                    )
                )
                .Where(predicate: genreRow => genreRow != null);
        }

        return genres.Where(predicate: genre => genre.Items.Any()).ToList();
    }

    private static GenreRowItemDto? TransformToRowItemDto(
        string country,
        HomeSourceDto source,
        List<HomeTvCardDto> tvData,
        List<HomeMovieCardDto> movieData
    )
    {
        return source.MediaType switch
        {
            MediaTypes.TvMediaType => tvData.FirstOrDefault(predicate: t => t.Id == source.Id) is { } tv
                ? new GenreRowItemDto(tv: tv, country: country)
                : null,
            MediaTypes.MovieMediaType => movieData.FirstOrDefault(predicate: m => m.Id == source.Id)
                is { } movie
                ? new GenreRowItemDto(movie: movie, country: country)
                : null,
            _ => null,
        };
    }

    private IEnumerable<GenreRowDto<GenreRowItemDto>> FetchGenres(List<Genre> genreItems)
    {
        return from genre in genreItems
            let name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name
            select new GenreRowDto<GenreRowItemDto>
            {
                Title = name,
                MoreLink = new(uriString: $"/genres/{genre.Id}", uriKind: UriKind.Relative),
                Id = genre.Id.ToString(),
                Source = genre
                    .GenreMovies.Select(selector: movie => new HomeSourceDto(
                        id: movie.MovieId,
                        type: MediaTypes.MovieMediaType
                    ))
                    .Concat(
                        second: genre.GenreTvShows.Select(selector: tv => new HomeSourceDto(
                            id: tv.TvId,
                            type: MediaTypes.TvMediaType
                        ))
                    )
                    .Randomize()
                    .Take(count: UiLimits.MaximumCardsInCarousel),
            };
    }

    public async Task<ComponentResponse> GetHomeData(Guid userId, string language, string country)
    {
        // Phase 1: Run initial independent queries in parallel — repository owns each DbContext
        HomeParallelData parallelData = await homeRepository.GetHomeParallelDataAsync(
            userId: userId,
            language: language,
            country: country
        );

        HashSet<UserData> continueWatching = parallelData.ContinueWatching;
        List<GenreHomeDto> genreItems = parallelData.GenreItems;
        List<Library> libraries = parallelData.Libraries;
        int animeCount = parallelData.AnimeCount;
        int movieCount = parallelData.MovieCount;
        int tvCount = parallelData.TvCount;

        // Early-exit: return an NMEmptyState component when there is nothing to show
        bool hasNoContent = movieCount == 0 && tvCount == 0 && animeCount == 0;

        if (hasNoContent)
        {
            ComponentEnvelope emptyState =
                libraries.Count == 0
                    ? Component
                        .EmptyState(
                            data: new()
                            {
                                Title = "No libraries yet",
                                Message = "Create your first library to get started.",
                                Icon = "library",
                                Action = new()
                                {
                                    Label = "Add library",
                                    Route = "/dashboard/libraries",
                                },
                            }
                        )
                        .Build()
                    : Component
                        .EmptyState(
                            data: new()
                            {
                                Title = "Scanning your libraries",
                                Message =
                                    "Content will appear as it's found. This usually takes a few minutes.",
                                Icon = "scanning",
                                AutoRefresh = true,
                            }
                        )
                        .Build();

            return new() { Data = [emptyState] };
        }

        // Phase 2: Collect genre source data (sync, fast - just shuffling IDs)
        List<GenreSourceData> genreSourceList = [];
        List<int> movieIds = [];
        List<int> tvIds = [];

        foreach (GenreHomeDto genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies = genre.MovieIds.Select(selector: id => new HomeSourceDto(
                id: id,
                type: MediaTypes.MovieMediaType
            ));
            IEnumerable<HomeSourceDto> tvs = genre.TvIds.Select(selector: id => new HomeSourceDto(
                id: id,
                type: MediaTypes.TvMediaType
            ));

            string name = genre.TranslatedName ?? genre.Name;
            List<HomeSourceDto> source = movies
                .Concat(second: tvs)
                .Randomize()
                .Take(count: UiLimits.MaximumCardsInCarousel)
                .ToList();

            tvIds.AddRange(
                collection: source.Where(predicate: s => s.MediaType == MediaTypes.TvMediaType).Select(selector: s => s.Id)
            );
            movieIds.AddRange(
                collection: source.Where(predicate: s => s.MediaType == MediaTypes.MovieMediaType).Select(selector: s => s.Id)
            );

            genreSourceList.Add(
                item: new(Id: genre.Id.ToString(), Title: name, MoreLink: new(uriString: $"/genres/{genre.Id}", uriKind: UriKind.Relative), Source: source)
            );
        }

        // Phase 3: Fetch genre media data and, in parallel, the per-library
        // "Latest in {library}" cards. Each repository call owns its own context,
        // so the fan-out stays concurrency-safe without the service touching a
        // DbContext.
        Task<HomeTvsAndMoviesData> tvsAndMoviesTask = homeRepository.GetHomeTvsAndMoviesAsync(
            tvIds: tvIds,
            movieIds: movieIds,
            language: language,
            country: country
        );

        List<
            Task<(Library Library, List<MovieCardDto> Movies, List<TvCardDto> Shows)>
        > libraryTasks = libraries
            .Select(selector: async library =>
            {
                List<MovieCardDto> libraryMovies =
                    await libraryRepository.GetLibraryMovieCardsAsync(
                        userId: userId,
                        libraryId: library.Id,
                        country: country,
                        take: UiLimits.MaximumCardsInCarousel,
                        skip: 0
                    );
                List<TvCardDto> libraryShows = await libraryRepository.GetLibraryTvCardsAsync(
                    userId: userId,
                    libraryId: library.Id,
                    country: country,
                    take: UiLimits.MaximumCardsInCarousel,
                    skip: 0
                );
                return (library, libraryMovies, libraryShows);
            })
            .ToList();

        await Task.WhenAll(tasks: [tvsAndMoviesTask, Task.WhenAll(tasks: libraryTasks)]);

        HomeTvsAndMoviesData tvsAndMovies = tvsAndMoviesTask.Result;
        List<HomeTvCardDto> tvData = tvsAndMovies.TvData;
        List<HomeMovieCardDto> movieData = tvsAndMovies.MovieData;

        // Build genre carousels with resolved items
        List<GenreCarouselData> genreCarousels = genreSourceList
            .Select(selector: g => new GenreCarouselData(
                Id: g.Id,
                Title: g.Title,
                MoreLink: g.MoreLink,
                Items: g.Source.Select(selector: source => ResolveCardData(source: source, tvData: tvData, movieData: movieData, country: country))
                    .Where(predicate: c => c != null)
                    .Cast<CardData>()
                    .ToList()
            ))
            .Where(predicate: g => g.Items.Count > 0)
            .ToList();

        // Get random home card
        CardData? homeCardItem = genreCarousels
            .Where(predicate: g => !string.IsNullOrEmpty(value: g.Title))
            .SelectMany(selector: g => g.Items)
            .Where(predicate: c => !string.IsNullOrWhiteSpace(value: c.Title))
            .Randomize()
            .FirstOrDefault();

        // Build library carousels from projection results (empty unless the
        // desktop surface asked for them).
        List<GenreCarouselData> libraryCarousels = [];

        foreach (
            (
                Library library,
                List<MovieCardDto> libraryMovies,
                List<TvCardDto> libraryShows
            ) in libraryTasks.Select(selector: t => t.Result)
        )
        {
            bool shouldPaginate =
                (
                    library.Type == MediaTypes.MovieMediaType
                    && movieCount > UiLimits.MaximumItemsPerPage
                )
                || (
                    library.Type == MediaTypes.TvMediaType && tvCount > UiLimits.MaximumItemsPerPage
                )
                || (
                    library.Type == MediaTypes.AnimeMediaType
                    && animeCount > UiLimits.MaximumItemsPerPage
                );

            List<CardData> items = libraryMovies
                .Select(selector: m => new CardData(movie: m, country: country))
                .Concat(second: libraryShows.Select(selector: t => new CardData(tv: t, country: country)))
                .OrderByDescending(keySelector: c => c.CreatedAt)
                .ToList();

            if (items.Count > 0)
            {
                Uri moreLink = shouldPaginate
                    ? new(uriString: $"/libraries/{library.Id}/letter/A", uriKind: UriKind.Relative)
                    : new Uri(uriString: $"/libraries/{library.Id}", uriKind: UriKind.Relative);

                libraryCarousels.Add(item: new(Id: library.Id.ToString(), Title: library.Title, MoreLink: moreLink, Items: items));
            }
        }

        // Build components
        List<ComponentEnvelope> components = [];

        // Home card
        if (homeCardItem != null)
        {
            components.Add(
                item: Component
                    .HomeCard(
                        data: new()
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
                            MediaType = homeCardItem.Type,
                        }
                    )
                    .WithUpdate(when: "pageLoad", link: "/home/card")
                    .Build()
            );
        }

        // Navigation chain: continue → genre_* → continue (circular)
        bool hasContinueWatching = continueWatching.Count > 0;
        string? continueId = hasContinueWatching ? "continue" : null;

        string? lastCarouselId =
            genreCarousels.Count > 0 ? $"genre_{genreCarousels[^1].Id}"
            : libraryCarousels.Count > 0 ? $"library_{libraryCarousels[^1].Id}"
            : null;

        string? afterContinueId =
            libraryCarousels.Count > 0 ? $"library_{libraryCarousels[index: 0].Id}"
            : genreCarousels.Count > 0 ? $"genre_{genreCarousels[index: 0].Id}"
            : null;

        // Continue watching carousel (only when there are items to show)
        if (hasContinueWatching)
        {
            components.Add(
                item: Component
                    .Carousel()
                    .WithId(id: "continue")
                    .WithNavigation(previousId: lastCarouselId, nextId: afterContinueId)
                    .WithTitle(title: "Continue watching".Localize())
                    .WithUpdate(when: "pageLoad", link: "/home/continue")
                    .WithItems(items: BuildContinueWatchingCards(continueWatching: continueWatching, country: country))
                    .Build()
            );
        }

        // Library "Latest in {library}" carousels (between continue and genres)
        for (int i = 0; i < libraryCarousels.Count; i++)
        {
            GenreCarouselData lib = libraryCarousels[index: i];

            string? prevId = i == 0 ? continueId : $"library_{libraryCarousels[index: i - 1].Id}";
            string? nextId =
                i == libraryCarousels.Count - 1
                    ? genreCarousels.Count > 0
                        ? $"genre_{genreCarousels[index: 0].Id}"
                        : continueId
                    : $"library_{libraryCarousels[index: i + 1].Id}";

            components.Add(
                item: Component
                    .Carousel()
                    .WithId(id: $"library_{lib.Id}")
                    .WithNavigation(previousId: prevId, nextId: nextId)
                    .WithTitle(title: $"Latest in {lib.Title}")
                    .WithMoreLink(moreLink: lib.MoreLink)
                    .WithItems(items: lib.Items.Select(selector: item => Component.Card(data: item).Build()))
                    .Build()
            );
        }

        // Genre carousels
        for (int i = 0; i < genreCarousels.Count; i++)
        {
            GenreCarouselData genre = genreCarousels[index: i];

            string? prevId =
                i == 0
                    ? libraryCarousels.Count > 0
                        ? $"library_{libraryCarousels[^1].Id}"
                        : continueId
                    : $"genre_{genreCarousels[index: i - 1].Id}";
            string? nextId =
                i == genreCarousels.Count - 1 ? continueId : $"genre_{genreCarousels[index: i + 1].Id}";

            components.Add(
                item: Component
                    .Carousel()
                    .WithId(id: $"genre_{genre.Id}")
                    .WithNavigation(previousId: prevId, nextId: nextId)
                    .WithTitle(title: genre.Title)
                    .WithMoreLink(moreLink: genre.MoreLink)
                    .WithItems(items: genre.Items.Select(selector: item => Component.Card(data: item).Build()))
                    .Build()
            );
        }

        return new() { Data = components };
    }

    private static CardData? ResolveCardData(
        HomeSourceDto source,
        List<HomeTvCardDto> tvData,
        List<HomeMovieCardDto> movieData,
        string country,
        bool watch = false
    )
    {
        return source.MediaType switch
        {
            MediaTypes.TvMediaType => tvData.FirstOrDefault(predicate: t => t.Id == source.Id) is { } tv
                ? new CardData(tv: tv, country: country, watch: watch)
                : null,
            MediaTypes.MovieMediaType => movieData.FirstOrDefault(predicate: m => m.Id == source.Id)
                is { } movie
                ? new CardData(movie: movie, country: country, watch: watch)
                : null,
            _ => null,
        };
    }

    private static IEnumerable<ComponentEnvelope> BuildContinueWatchingCards(
        IEnumerable<UserData> continueWatching,
        string country
    )
    {
        return continueWatching
            .Select(selector: item =>
                Component
                    .Card(data: new(item: item, country: country))
                    .WithWatch()
                    .WithContextMenu(items:
                    [
                        new()
                        {
                            Id = "remove_continue_watching",
                            Title = "Remove from continue watching".Localize(),
                            Icon = "mooooom-trash",
                            Method = "DELETE",
                            Destructive = true,
                            Confirm =
                                "Are you sure you want to remove this from continue watching?".Localize(),
                            Args = new()
                            {
                                { "url", "/userData/continue" },
                                { "replaceKey", "home" },
                            },
                        },
                    ])
                    .Build()
            )
            .DistinctBy(keySelector: c => ((LeafProps<CardData>)c.Props).Data?.Link);
    }

    public async Task<ComponentResponse> GetHomeCard(
        Guid userId,
        string language,
        string country,
        Ulid replaceId
    )
    {
        HomeTvCardDto? tv = await libraryRepository.GetRandomTvCardAsync(userId: userId, language: language, country: country);
        HomeMovieCardDto? movie = await libraryRepository.GetRandomMovieCardAsync(
            userId: userId,
            language: language,
            country: country
        );

        List<CardData> candidates = [];
        if (tv != null)
            candidates.Add(item: new(tv: tv, country: country));
        if (movie != null)
            candidates.Add(item: new(movie: movie, country: country));

        CardData? homeCardItem = candidates
            .Where(predicate: c => !string.IsNullOrWhiteSpace(value: c.Title))
            .Randomize()
            .FirstOrDefault();

        return new()
        {
            Data =
            [
                Component
                    .HomeCard(
                        data: homeCardItem != null
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
                                MediaType = homeCardItem.Type,
                            }
                            : new HomeCardData()
                    )
                    .WithUpdate(when: "pageLoad", link: "/home/card")
                    .WithReplacing(replacingId: replaceId)
                    .Build(),
            ],
        };
    }

    public async Task<ScreensaverDto> GetSetupScreensaverContent(Guid userId)
    {
        HashSet<Image> data = await homeRepository.GetScreensaverImagesAsync(userId: userId);

        // Logo lookups built once. The old per-backdrop FirstOrDefault over a lazy
        // logo filter re-scanned every image for each backdrop (O(backdrops x images)),
        // seconds of CPU on a large library. Index the logos by title id instead.
        Dictionary<int, Image> logoByTv = data.Where(predicate: image =>
                image is { Type: "logo", TvId: not null }
            )
            .GroupBy(keySelector: image => image.TvId!.Value)
            .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First());
        Dictionary<int, Image> logoByMovie = data.Where(predicate: image =>
                image is { Type: "logo", MovieId: not null }
            )
            .GroupBy(keySelector: image => image.MovieId!.Value)
            .ToDictionary(keySelector: group => group.Key, elementSelector: group => group.First());

        IEnumerable<ScreensaverDataDto> tvCollection = data.Where(predicate: image =>
                image is { TvId: not null, Type: "backdrop" }
            )
            .DistinctBy(keySelector: image => image.TvId)
            .Select(selector: image => new ScreensaverDataDto(
                image: image,
                logo: logoByTv.GetValueOrDefault(key: image.TvId!.Value)
            ));

        IEnumerable<ScreensaverDataDto> movieCollection = data.Where(predicate: image =>
                image is { MovieId: not null, Type: "backdrop" }
            )
            .DistinctBy(keySelector: image => image.MovieId)
            .Select(selector: image => new ScreensaverDataDto(
                image: image,
                logo: logoByMovie.GetValueOrDefault(key: image.MovieId!.Value)
            ));

        return new()
        {
            Data = tvCollection
                .Concat(second: movieCollection)
                .Where(predicate: image => image.Meta?.Logo != null)
                .Randomize(),
        };
    }

    public async Task<ComponentResponse> GetHomeTvContent(
        Guid userId,
        string language,
        string country
    )
    {
        HashSet<UserData> continueWatching = await homeRepository.GetContinueWatchingAsync(
            userId: userId,
            language: language,
            country: country
        );

        // Collect genre source data
        List<GenreSourceData> genreSourceList = [];
        List<int> movieIds = [];
        List<int> tvIds = [];

        List<GenreHomeDto> genreItems = await homeRepository.GetHomeGenresAsync(
            userId: userId,
            language: language,
            take: UiLimits.MaximumItemsPerPage
        );

        foreach (GenreHomeDto genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies = genre.MovieIds.Select(selector: id => new HomeSourceDto(
                id: id,
                type: MediaTypes.MovieMediaType
            ));
            IEnumerable<HomeSourceDto> tvs = genre.TvIds.Select(selector: id => new HomeSourceDto(
                id: id,
                type: MediaTypes.TvMediaType
            ));

            string name = genre.TranslatedName ?? genre.Name;
            List<HomeSourceDto> source = movies
                .Concat(second: tvs)
                .Randomize()
                .Take(count: UiLimits.MaximumCardsInCarousel)
                .ToList();

            tvIds.AddRange(
                collection: source.Where(predicate: s => s.MediaType == MediaTypes.TvMediaType).Select(selector: s => s.Id)
            );
            movieIds.AddRange(
                collection: source.Where(predicate: s => s.MediaType == MediaTypes.MovieMediaType).Select(selector: s => s.Id)
            );

            genreSourceList.Add(
                item: new(Id: genre.Id.ToString(), Title: name, MoreLink: new(uriString: $"/genres/{genre.Id}", uriKind: UriKind.Relative), Source: source)
            );
        }

        // Fetch data
        HomeTvsAndMoviesData tvsAndMovies = await homeRepository.GetHomeTvsAndMoviesAsync(
            tvIds: tvIds,
            movieIds: movieIds,
            language: language,
            country: country
        );

        // Build genre carousels
        List<GenreCarouselData> genreCarousels = genreSourceList
            .Select(selector: g => new GenreCarouselData(
                Id: g.Id,
                Title: g.Title,
                MoreLink: g.MoreLink,
                Items: g.Source.Select(selector: source =>
                        ResolveCardData(
                            source: source,
                            tvData: tvsAndMovies.TvData,
                            movieData: tvsAndMovies.MovieData,
                            country: country,
                            watch: false
                        )
                    )
                    .Where(predicate: c => c != null)
                    .Cast<CardData>()
                    .ToList()
            ))
            .Where(predicate: g => g.Items.Count > 0)
            .ToList();

        // Build components
        List<ComponentEnvelope> components = [];

        // Continue watching
        components.Add(
            item: Component
                .Carousel()
                .WithId(id: "continue")
                .WithTitle(title: "Continue watching".Localize())
                .WithUpdate(when: "pageLoad", link: "/home/continue")
                .WithItems(items: BuildContinueWatchingCards(continueWatching: continueWatching, country: country))
                .Build()
        );

        // Genre carousels (limited to 6 items for TV)
        foreach (GenreCarouselData genre in genreCarousels)
        {
            components.Add(
                item: Component
                    .Carousel()
                    .WithId(id: $"genre_{genre.Id}")
                    .WithTitle(title: genre.Title)
                    .WithMoreLink(moreLink: genre.MoreLink)
                    .WithItems(items: genre.Items.Take(count: 6).Select(selector: item => Component.Card(data: item).Build()))
                    .Build()
            );
        }

        return new() { Data = components };
    }

    public async Task<ComponentResponse> GetHomeContinueContent(
        Guid userId,
        string language,
        string country,
        Ulid replaceId
    )
    {
        HashSet<UserData> continueWatching = await homeRepository.GetContinueWatchingAsync(
            userId: userId,
            language: language,
            country: country
        );

        IEnumerable<UserData> filtered = continueWatching.Where(predicate: item =>
            item.Tv?.Episodes.LastOrDefault()?.VideoFiles.FirstOrDefault()?.Id != item.VideoFileId
            || item.Time < (item.VideoFile.Duration?.ToSeconds() ?? 0) * 0.8
        );

        return new()
        {
            Data =
            [
                Component
                    .Carousel()
                    .WithId(id: "continue")
                    .WithNavigation(previousId: "continue", nextId: "28")
                    .WithTitle(title: "Continue watching".Localize())
                    .WithUpdate(when: "pageLoad", link: "/home/continue")
                    .WithItems(items: BuildContinueWatchingCards(continueWatching: filtered, country: country))
                    .WithReplacing(replacingId: replaceId)
                    .Build(),
            ],
        };
    }

    // Helper records for intermediate data
    private record GenreSourceData(
        string Id,
        string Title,
        Uri MoreLink,
        List<HomeSourceDto> Source
    );

    private record GenreCarouselData(string Id, string Title, Uri MoreLink, List<CardData> Items);
}
