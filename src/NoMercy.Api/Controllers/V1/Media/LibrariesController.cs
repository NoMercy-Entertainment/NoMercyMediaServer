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

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Authorization;
using NoMercy.Data.DTOs.Specials;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Libraries")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/libraries")]
public class LibrariesController(
    ILibraryRepository libraryRepository,
    IDbContextFactory<MediaContext> contextFactory
) : BaseController
{
    [HttpGet]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Libraries(CancellationToken ct = default)
    {
        Guid userId = User.UserId();

        List<LibrariesResponseItemDto> response = (await libraryRepository.GetLibraries(userId: userId, ct: ct))
            .Select(selector: library => new LibrariesResponseItemDto(library: library))
            .ToList();

        return Ok(value: new LibrariesDto { Data = response.OrderBy(keySelector: library => library.Order) });
    }

    [HttpGet]
    [Route(template: "mobile")]
    public async Task<IActionResult> Mobile(CancellationToken ct = default)
    {
        Guid userId = User.UserId();

        string language = Language();
        string country = Country();

        // Start all independent queries in parallel - each task gets its own DbContext for thread safety
        Task<List<Library>> librariesTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetLibrariesLite(userId: userId, ct: ct);
        }, cancellationToken: ct);
        Task<Dictionary<Ulid, int>> countsTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetLibraryItemCountsAsync(
                userId: userId,
                ct: ct
            );
        }, cancellationToken: ct);
        Task<List<CollectionListDto>> collectionsTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new CollectionRepository(contextFactory: contextFactory).GetCollectionItemCardsAsync(
                userId: userId,
                language: language,
                country: country,
                take: 10,
                page: 0,
                ct: ct
            );
        }, cancellationToken: ct);
        Task<List<SpecialCardDto>> specialsTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new SpecialRepository(context: ctx, contextFactory: contextFactory).GetSpecialItemCardsAsync(
                userId: userId,
                language: language,
                country: country,
                take: 10,
                page: 0,
                ct: ct
            );
        }, cancellationToken: ct);
        Task<HomeTvCardDto?> randomTvTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetRandomTvCardAsync(
                userId: userId,
                language: language,
                country: country,
                ct: ct
            );
        }, cancellationToken: ct);
        Task<HomeMovieCardDto?> randomMovieTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetRandomMovieCardAsync(
                userId: userId,
                language: language,
                country: country,
                ct: ct
            );
        }, cancellationToken: ct);
        Task<FavoritesData> favoritesTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new HomeRepository(context: ctx, contextFactory: contextFactory).GetFavoritesAsync(
                userId: userId,
                language: language,
                country: country,
                ct: ct
            );
        }, cancellationToken: ct);
        Task<List<UserPlaylistSummary>> myListsTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new UserPlaylistRepository(contextFactory: contextFactory).GetUserPlaylistsAsync(
                userId: userId,
                ct: ct
            );
        }, cancellationToken: ct);

        await Task.WhenAll(tasks: [librariesTask, countsTask, collectionsTask, specialsTask, randomTvTask, randomMovieTask, favoritesTask, myListsTask]
        );

        List<Library> libraries = librariesTask.Result;
        Dictionary<Ulid, int> itemCounts = countsTask.Result;
        List<CollectionListDto> collections = collectionsTask.Result;
        List<SpecialCardDto> specials = specialsTask.Result;
        HomeTvCardDto? tv = randomTvTask.Result;
        HomeMovieCardDto? movie = randomMovieTask.Result;
        FavoritesData favorites = favoritesTask.Result;
        List<UserPlaylistSummary> myLists = myListsTask.Result;

        List<NmCardDto> favoriteCards =
        [
            .. favorites.Movies.Select(selector: favoriteMovie => new NmCardDto(movie: favoriteMovie, country: country)),
            .. favorites.TvShows.Select(selector: favoriteTv => new NmCardDto(tv: favoriteTv, country: country)),
            .. favorites.Collections.Select(selector: favoriteCollection => new NmCardDto(
                collection: favoriteCollection,
                country: country
            )),
            .. favorites.Specials.Select(selector: favoriteSpecial => new NmCardDto(
                special: favoriteSpecial,
                country: country
            )),
        ];
        favoriteCards = favoriteCards
            .OrderBy(keySelector: card => card.Title, comparer: StringComparer.OrdinalIgnoreCase)
            .DistinctBy(keySelector: card => card.Link)
            .ToList();

        List<NmCardDto> myListCards = myLists
            .Select(selector: summary => new NmCardDto
            {
                Id = summary.Id,
                Title = summary.Name,
                Poster = summary.Cover,
                Link = new(uriString: $"/lists/{summary.Id}", uriKind: UriKind.Relative),
                Type = "playlist",
                NumberOfItems = summary.ItemCount,
                HaveItems = summary.ItemCount,
            })
            .ToList();

        // Fetch library data in parallel - each task gets its own DbContext for thread safety
        Library[] nonMusicLibraries = libraries.Where(predicate: lib => lib.Type != "music").ToArray();

        Task<(
            Library library,
            List<MovieCardDto> movies,
            List<TvCardDto> shows
        )>[] libraryDataTasks = nonMusicLibraries
            .Select(selector: async library =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                LibraryRepository repo = new(contextFactory: contextFactory);
                List<MovieCardDto> movies = await repo.GetLibraryMovieCardsAsync(
                    userId: userId,
                    libraryId: library.Id,
                    country: country,
                    take: 10,
                    skip: 0,
                    ct: ct
                );
                List<TvCardDto> shows = await repo.GetLibraryTvCardsAsync(
                    userId: userId,
                    libraryId: library.Id,
                    country: country,
                    take: 10,
                    skip: 0,
                    ct: ct
                );
                return (library, movies, shows);
            })
            .ToArray();

        (Library library, List<MovieCardDto> movies, List<TvCardDto> shows)[] libraryDataResults =
            await Task.WhenAll(tasks: libraryDataTasks);

        List<NmCarouselDto<NmCardDto>> list = [];

        foreach (
            (
                Library library,
                List<MovieCardDto> libraryMovies,
                List<TvCardDto> libraryShows
            ) in libraryDataResults
        )
        {
            int totalItems = itemCounts.GetValueOrDefault(key: library.Id);
            Uri moreLink =
                totalItems > 500
                    ? new(uriString: $"/libraries/{library.Id}/letter/A", uriKind: UriKind.Relative)
                    : new(uriString: $"/libraries/{library.Id}", uriKind: UriKind.Relative);

            list.Add(
                item: new()
                {
                    Title = library.Title,
                    MoreLink = moreLink,
                    Items = libraryMovies
                        .Select(selector: m => new NmCardDto(movie: m, country: country))
                        .Concat(second: libraryShows.Select(selector: t => new NmCardDto(tv: t, country: country)))
                        .ToList(),
                }
            );
        }

        list.Add(
            item: new()
            {
                Title = "Favorites",
                MoreLink = new(uriString: "/favorites", uriKind: UriKind.Relative),
                Items = favoriteCards,
            }
        );

        list.Add(
            item: new()
            {
                Title = "My Lists",
                MoreLink = new(uriString: "/lists", uriKind: UriKind.Relative),
                Items = myListCards,
            }
        );

        list.Add(
            item: new()
            {
                Title = "Collections",
                MoreLink = new(uriString: "/collection", uriKind: UriKind.Relative),
                Items = collections
                    .Select(selector: collection => new NmCardDto(dto: collection, country: country))
                    .ToList(),
            }
        );

        list.Add(
            item: new()
            {
                Title = "Specials",
                MoreLink = new(uriString: "/specials", uriKind: UriKind.Relative),
                Items = specials.Select(selector: special => new NmCardDto(dto: special, country: country)).ToList(),
            }
        );

        List<NmCardDto> genres = [];
        if (tv != null)
            genres.Add(item: new(tv: tv, country: country));

        if (movie != null)
            genres.Add(item: new(movie: movie, country: country));

        NmCardDto? homeCardItem = genres
            .Where(predicate: g => !string.IsNullOrWhiteSpace(value: g.Title))
            .Randomize()
            .FirstOrDefault();

        List<ComponentEnvelope> components = new();

        // Add home card
        if (homeCardItem != null)
        {
            HomeCardData homeCardData = new(cardDto: homeCardItem);
            dynamic? homeCard = Component
                .HomeCard()
                .WithId(id: "home_card")
                .WithTitle(title: homeCardData.Title)
                .WithData(data: homeCardData)
                .WithUpdate(when: "pageLoad", link: "/home/card")
                .Build();

            components.Add(item: homeCard);
        }

        // Add carousels for each library
        for (int index = 0; index < list.Count; index++)
        {
            NmCarouselDto<NmCardDto> carouselData = list[index: index];
            ComponentEnvelope carousel = Component
                .Carousel()
                .WithId(id: $"library_{carouselData.Id}")
                .WithTitle(title: carouselData.Title)
                .WithMoreLink(moreLink: carouselData.MoreLink)
                .WithNavigation(
                    previousId: index == 0 ? "home_card" : $"library_{list[index: index - 1].Id}",
                    nextId: index == list.Count - 1 ? null : $"library_{list[index: index + 1].Id}"
                )
                .WithItems(builders: carouselData.Items.Select(selector: item => Component.Card().WithData(data: new(dto: item))));

            components.Add(item: carousel);
        }

        return Ok(value: ComponentResponse.From(components: components));
    }

    [HttpGet]
    [Route(template: "tv")]
    public async Task<IActionResult> Tv(CancellationToken ct = default)
    {
        Guid userId = User.UserId();

        string language = Language();
        string country = Country();

        // Start all independent queries in parallel - each task gets its own DbContext for thread safety
        Task<List<Library>> librariesTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetLibrariesLite(userId: userId, ct: ct);
        });
        Task<List<CollectionListDto>> collectionsTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new CollectionRepository(contextFactory: contextFactory).GetCollectionItemCardsAsync(
                userId: userId,
                language: language,
                country: country,
                take: 6,
                page: 0,
                ct: ct
            );
        });
        Task<List<SpecialCardDto>> specialsTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new SpecialRepository(context: ctx, contextFactory: contextFactory).GetSpecialItemCardsAsync(
                userId: userId,
                language: language,
                country: country,
                take: 6,
                page: 0,
                ct: ct
            );
        });
        Task<HomeTvCardDto?> randomTvTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetRandomTvCardAsync(
                userId: userId,
                language: language,
                country: country,
                ct: ct
            );
        });
        Task<HomeMovieCardDto?> randomMovieTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetRandomMovieCardAsync(
                userId: userId,
                language: language,
                country: country,
                ct: ct
            );
        });
        Task<FavoritesData> favoritesTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new HomeRepository(context: ctx, contextFactory: contextFactory).GetFavoritesAsync(
                userId: userId,
                language: language,
                country: country,
                ct: ct
            );
        });
        Task<List<UserPlaylistSummary>> myListsTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new UserPlaylistRepository(contextFactory: contextFactory).GetUserPlaylistsAsync(
                userId: userId,
                ct: ct
            );
        });

        await Task.WhenAll(tasks: [librariesTask, collectionsTask, specialsTask, randomTvTask, randomMovieTask, favoritesTask, myListsTask]
        );

        List<Library> libraries = librariesTask.Result;
        List<CollectionListDto> collections = collectionsTask.Result;
        List<SpecialCardDto> specials = specialsTask.Result;
        HomeTvCardDto? tv = randomTvTask.Result;
        HomeMovieCardDto? movie = randomMovieTask.Result;
        FavoritesData favorites = favoritesTask.Result;
        List<UserPlaylistSummary> myLists = myListsTask.Result;

        List<NmCardDto> favoriteCards =
        [
            .. favorites.Movies.Select(selector: favoriteMovie => new NmCardDto(movie: favoriteMovie, country: country)),
            .. favorites.TvShows.Select(selector: favoriteTv => new NmCardDto(tv: favoriteTv, country: country)),
            .. favorites.Collections.Select(selector: favoriteCollection => new NmCardDto(
                collection: favoriteCollection,
                country: country
            )),
            .. favorites.Specials.Select(selector: favoriteSpecial => new NmCardDto(
                special: favoriteSpecial,
                country: country
            )),
        ];
        favoriteCards = favoriteCards
            .OrderBy(keySelector: card => card.Title, comparer: StringComparer.OrdinalIgnoreCase)
            .DistinctBy(keySelector: card => card.Link)
            .ToList();

        List<NmCardDto> myListCards = myLists
            .Select(selector: summary => new NmCardDto
            {
                Id = summary.Id,
                Title = summary.Name,
                Poster = summary.Cover,
                Link = new(uriString: $"/lists/{summary.Id}", uriKind: UriKind.Relative),
                Type = "playlist",
                NumberOfItems = summary.ItemCount,
                HaveItems = summary.ItemCount,
            })
            .ToList();

        // Fetch library data in parallel - each task gets its own DbContext for thread safety
        Task<(
            Library library,
            List<MovieCardDto> movies,
            List<TvCardDto> shows
        )>[] libraryDataTasks = libraries
            .Select(selector: async library =>
            {
                await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
                LibraryRepository repo = new(contextFactory: contextFactory);
                List<MovieCardDto> movies = await repo.GetLibraryMovieCardsAsync(
                    userId: userId,
                    libraryId: library.Id,
                    country: country,
                    take: 6,
                    skip: 0,
                    ct: ct
                );
                List<TvCardDto> shows = await repo.GetLibraryTvCardsAsync(
                    userId: userId,
                    libraryId: library.Id,
                    country: country,
                    take: 6,
                    skip: 0,
                    ct: ct
                );
                return (library, movies, shows);
            })
            .ToArray();

        (Library library, List<MovieCardDto> movies, List<TvCardDto> shows)[] libraryDataResults =
            await Task.WhenAll(tasks: libraryDataTasks);

        List<NmCarouselDto<NmCardDto>> list = [];

        foreach (
            (
                Library library,
                List<MovieCardDto> libraryMovies,
                List<TvCardDto> libraryShows
            ) in libraryDataResults
        )
        {
            list.Add(
                item: new()
                {
                    Id = "library_" + library.Id,
                    Title = library.Title,
                    MoreLink = new(uriString: $"/libraries/{library.Id}", uriKind: UriKind.Relative),
                    Items = libraryMovies
                        .Select(selector: m => new NmCardDto(movie: m, country: country))
                        .Concat(second: libraryShows.Select(selector: t => new NmCardDto(tv: t, country: country)))
                        .ToList(),
                }
            );
        }

        list.Add(
            item: new()
            {
                Id = "library_favorites",
                Title = "Favorites",
                MoreLink = new(uriString: "/favorites", uriKind: UriKind.Relative),
                Items = favoriteCards,
            }
        );

        list.Add(
            item: new()
            {
                Id = "library_lists",
                Title = "My Lists",
                MoreLink = new(uriString: "/lists", uriKind: UriKind.Relative),
                Items = myListCards,
            }
        );

        list.Add(
            item: new()
            {
                Id = "library_collections",
                Title = "Collections",
                MoreLink = new(uriString: "/collection", uriKind: UriKind.Relative),
                Items = collections
                    .Select(selector: collection => new NmCardDto(dto: collection, country: country))
                    .ToList(),
            }
        );

        list.Add(
            item: new()
            {
                Id = "library_specials",
                Title = "Specials",
                MoreLink = new(uriString: "/specials", uriKind: UriKind.Relative),
                Items = specials.Select(selector: special => new NmCardDto(dto: special, country: country)).ToList(),
            }
        );

        List<NmCardDto> genres = [];
        if (tv != null)
            genres.Add(item: new(tv: tv, country: country));

        if (movie != null)
            genres.Add(item: new(movie: movie, country: country));

        List<ComponentEnvelope> components = new();

        // Add carousels for each library
        for (int index = 0; index < list.Count; index++)
        {
            NmCarouselDto<NmCardDto> carouselData = list[index: index];
            dynamic? carousel = Component
                .Carousel()
                .WithId(id: carouselData.Id)
                .WithTitle(carouselData.Title)
                .WithMoreLink(carouselData.MoreLink)
                .WithNavigation(
                    index == 0 ? "home_card" : list[index: index - 1].Id,
                    index == list.Count - 1 ? null : list[index: index + 1].Id
                )
                .WithItems(
                    carouselData.Items.Take(count: 6).Select(selector: item => Component.Card().WithData(data: new(dto: item)))
                );
            components.Add(item: carousel);
        }

        return Ok(value: ComponentResponse.From(components: components));
    }

    [HttpGet]
    [Route(template: "{libraryId:ulid}")]
    public async Task<IActionResult> Library(
        Ulid libraryId,
        [FromQuery] PageRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        string language = Language();
        string country = Country();

        // Fetch movies and shows in parallel - each task gets its own DbContext for thread safety
        Task<List<MovieCardDto>> moviesTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetLibraryMovieCardsAsync(
                userId: userId,
                libraryId: libraryId,
                country: country,
                take: request.Take,
                skip: request.Page * request.Take,
                ct: ct
            );
        });
        Task<List<TvCardDto>> showsTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetLibraryTvCardsAsync(
                userId: userId,
                libraryId: libraryId,
                country: country,
                take: request.Take,
                skip: request.Page * request.Take,
                ct: ct
            );
        });

        await Task.WhenAll(tasks: [moviesTask, showsTask]);

        List<MovieCardDto> libraryMovies = moviesTask.Result;
        List<TvCardDto> libraryShows = showsTask.Result;

        if (request.Version != "lolomo")
        {
            List<CardData> cardItems = libraryMovies
                .Select(selector: movie => new CardData(movie: movie, country: country))
                .Concat(second: libraryShows.Select(selector: tv => new CardData(tv: tv, country: country)))
                .OrderBy(keySelector: item => item.TitleSort)
                .ToList();

            ComponentEnvelope response = Component
                .Grid()
                .WithId(id: $"library-{libraryId}")
                .WithItems(builders: cardItems.Select(selector: item => Component.Card().WithData(data: item)));

            return Ok(value: ComponentResponse.From(component: response));
        }
        List<ComponentEnvelope> components = new();

        foreach (string letter in Letters)
        {
            int index = Array.IndexOf(array: Letters, value: letter);

            List<CardData> carouselItems = libraryMovies
                .Select(selector: movie => new CardData(movie: movie, country: country))
                .Where(predicate: collection => AlphaBucket.Matches(titleSort: collection.TitleSort, bucket: letter))
                .Concat(
                    second: libraryShows
                        .Select(selector: tv => new CardData(tv: tv, country: country))
                        .Where(predicate: collection => AlphaBucket.Matches(titleSort: collection.TitleSort, bucket: letter))
                )
                .OrderBy(keySelector: item => item.TitleSort)
                .ToList();

            if (carouselItems.Count == 0)
                continue;

            components.Add(
                item: Component
                    .Carousel()
                    .WithId(id: letter)
                    .WithTitle(title: letter)
                    .WithNavigation(
                        previousId: index == 0 ? null : Letters.ElementAtOrDefault(index: index - 1) ?? null,
                        nextId: index == Letters.Length - 1
                            ? null
                            : Letters.ElementAtOrDefault(index: index + 1) ?? null
                    )
                    .WithItems(builders: carouselItems.Select(selector: item => Component.Card().WithData(data: item)))
            );
        }

        return Ok(value: new ComponentResponse { Data = components });
    }

    [HttpGet]
    [Route(template: "{libraryId:ulid}/letter/{letter}")]
    public async Task<IActionResult> LibraryByLetter(
        Ulid libraryId,
        string letter,
        [FromQuery] PageRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        string language = Language();
        string country = Country();

        // Fetch movies and shows in parallel - each task gets its own DbContext for thread safety
        Task<List<HomeMovieCardDto>> moviesTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetPaginatedLibraryMovieCardsAsync(
                userId: userId,
                libraryId: libraryId,
                letter: letter,
                language: language,
                country: country,
                take: request.Take,
                page: request.Page,
                ct: ct
            );
        });
        Task<List<HomeTvCardDto>> showsTask = Task.Run(function: async () =>
        {
            await using MediaContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
            return await new LibraryRepository(contextFactory: contextFactory).GetPaginatedLibraryTvCardsAsync(
                userId: userId,
                libraryId: libraryId,
                letter: letter,
                language: language,
                country: country,
                take: request.Take,
                page: request.Page,
                ct: ct
            );
        });

        await Task.WhenAll(tasks: [moviesTask, showsTask]);

        List<HomeMovieCardDto> movies = moviesTask.Result;
        List<HomeTvCardDto> shows = showsTask.Result;

        List<CardData> concat = movies
            .Select(selector: movie => new CardData(movie: movie, country: country))
            .Concat(second: shows.Select(selector: tv => new CardData(tv: tv, country: country)))
            .OrderBy(keySelector: item => item.TitleSort)
            .ToList();

        ComponentEnvelope response = Component
            .Grid()
            .WithId(id: $"library-{libraryId}-{letter}")
            .WithTitle(title: letter)
            .WithItems(builders: concat.Select(selector: item => Component.Card().WithData(data: item)));

        return Ok(value: ComponentResponse.From(component: response));
    }

    /// Dead-letter review: media items that failed to import after all retries.
    /// Pass ?resolved=false to see only outstanding failures.
    [HttpGet]
    [Route(template: "{libraryId}/import-failures")]
    public async Task<IActionResult> ImportFailures(
        Ulid libraryId,
        [FromQuery] bool? resolved = null,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);

        IQueryable<ImportFailure> query = context.ImportFailures.Where(predicate: f =>
            f.LibraryId == libraryId
        );

        if (resolved is not null)
            query = query.Where(predicate: f => f.Resolved == resolved);

        List<ImportFailure> failures = (await query.ToListAsync(cancellationToken: ct))
            .OrderByDescending(keySelector: f => f.LastAttemptAt)
            .ToList();

        return Ok(value: new { data = failures });
    }
}
