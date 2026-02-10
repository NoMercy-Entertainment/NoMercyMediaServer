using System.Threading;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Dashboard.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;
using NoMercy.Helpers;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags("Media Libraries")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/libraries")]
public class LibrariesController(
    LibraryRepository libraryRepository,
    CollectionRepository collectionRepository,
    SpecialRepository specialRepository)
    : BaseController
{
    [HttpGet]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Libraries(CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        List<LibrariesResponseItemDto> response = (await libraryRepository.GetLibraries(userId, ct))
            .Select(library => new LibrariesResponseItemDto(library))
            .ToList();

        return Ok(new LibrariesDto
        {
            Data = response.OrderBy(library => library.Order)
        });
    }

    [HttpGet]
    [Route("mobile")]
    public async Task<IActionResult> Mobile(CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        string language = Language();
        string country = Country();

        // Start all independent queries in parallel
        Task<List<Library>> librariesTask = libraryRepository.GetLibraries(userId, ct);
        Task<List<Collection>> collectionsTask = collectionRepository.GetCollectionItems(userId, language, country, 10, 0, ct);
        Task<List<Special>> specialsTask = specialRepository.GetSpecialItems(userId, language, country, 10, 0, ct);
        Task<Tv?> randomTvTask = libraryRepository.GetRandomTvShow(userId, language, ct);
        Task<Movie?> randomMovieTask = libraryRepository.GetRandomMovie(userId, language, ct);

        await Task.WhenAll(librariesTask, collectionsTask, specialsTask, randomTvTask, randomMovieTask);

        List<Library> libraries = librariesTask.Result;
        List<Collection> collections = collectionsTask.Result;
        List<Special> specials = specialsTask.Result;
        Tv? tv = randomTvTask.Result;
        Movie? movie = randomMovieTask.Result;

        // Fetch library data in parallel for all non-music libraries using optimized projection queries
        Library[] nonMusicLibraries = libraries.Where(lib => lib.Type != "music").ToArray();

        Task<(Library library, List<MovieCardDto> movies, List<TvCardDto> shows)>[] libraryDataTasks = nonMusicLibraries
            .Select(async library =>
            {
                Task<List<MovieCardDto>> moviesTask = libraryRepository.GetLibraryMovieCardsAsync(userId, library.Id, country, 10, 0, ct);
                Task<List<TvCardDto>> showsTask = libraryRepository.GetLibraryTvCardsAsync(userId, library.Id, country, 10, 0, ct);
                await Task.WhenAll(moviesTask, showsTask);
                return (library, moviesTask.Result, showsTask.Result);
            })
            .ToArray();

        (Library library, List<MovieCardDto> movies, List<TvCardDto> shows)[] libraryDataResults = await Task.WhenAll(libraryDataTasks);

        List<NmCarouselDto<NmCardDto>> list = [];

        foreach ((Library library, List<MovieCardDto> libraryMovies, List<TvCardDto> libraryShows) in libraryDataResults)
        {
            Uri moreLink = library.LibraryMovies.Count + library.LibraryTvs.Count > 500
                ? new($"/libraries/{library.Id}/letter/A", UriKind.Relative)
                : new($"/libraries/{library.Id}", UriKind.Relative);

            list.Add(new()
            {
                Title = library.Title,
                MoreLink = moreLink,
                Items = libraryMovies.Select(m => new NmCardDto(m, country))
                    .Concat(libraryShows.Select(t => new NmCardDto(t, country)))
                    .ToList()
            });
        }

        list.Add(new()
        {
            Title = "Collections",
            MoreLink = new("/collection", UriKind.Relative),
            Items = collections.Select(collection => new NmCardDto(collection, country))
                .ToList()
        });

        list.Add(new()
        {
            Title = "Specials",
            MoreLink = new("/specials", UriKind.Relative),
            Items = specials
                .Select(special => new NmCardDto(special, country))
                .ToList()
        });

        List<NmCardDto> genres = [];
        if (tv != null)
            genres.Add(new(tv, language));

        if (movie != null)
            genres.Add(new(movie, language));

        NmCardDto? homeCardItem = genres.Where(g => !string.IsNullOrWhiteSpace(g.Title))
            .Randomize().FirstOrDefault();

        List<ComponentEnvelope> components = new();
        
        // Add home card
        if (homeCardItem != null)
        {
            HomeCardData homeCardData = new(homeCardItem);
            dynamic? homeCard = Component.HomeCard()
                .WithId("home_card")
                .WithTitle(homeCardData.Title)
                .WithData(homeCardData)
                .WithUpdate("pageLoad", "/home/card")
                .Build();

            components.Add(homeCard);
        }
        
        // Add carousels for each library
        for (int index = 0; index < list.Count; index++)
        {
            NmCarouselDto<NmCardDto> carouselData = list[index];
            ComponentEnvelope carousel = Component.Carousel()
                .WithId($"library_{carouselData.Id}")
                .WithTitle(carouselData.Title)
                .WithMoreLink(carouselData.MoreLink)
                .WithNavigation(
                    index == 0 ? "home_card" : $"library_{list[index - 1].Id}",
                    index == list.Count - 1 ? null : $"library_{list[index + 1].Id}")
                .WithItems(carouselData.Items.Select(item => Component.Card()
                    .WithData(new(item))));
            
            components.Add(carousel);
        }
        
        ComponentEnvelope response = Component.Container()
            .WithId("mobile-libraries")
            .WithItems(components);

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("tv")]
    public async Task<IActionResult> Tv(CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        string language = Language();
        string country = Country();

        // Start all independent queries in parallel
        Task<List<Library>> librariesTask = libraryRepository.GetLibraries(userId, ct);
        Task<List<Collection>> collectionsTask = collectionRepository.GetCollectionItems(userId, language, country, 6, 0, ct);
        Task<List<Special>> specialsTask = specialRepository.GetSpecialItems(userId, language, country, 6, 0, ct);
        Task<Tv?> randomTvTask = libraryRepository.GetRandomTvShow(userId, language, ct);
        Task<Movie?> randomMovieTask = libraryRepository.GetRandomMovie(userId, language, ct);

        await Task.WhenAll(librariesTask, collectionsTask, specialsTask, randomTvTask, randomMovieTask);

        List<Library> libraries = librariesTask.Result;
        List<Collection> collections = collectionsTask.Result;
        List<Special> specials = specialsTask.Result;
        Tv? tv = randomTvTask.Result;
        Movie? movie = randomMovieTask.Result;

        // Fetch library data in parallel for all libraries using optimized projection queries
        Task<(Library library, List<MovieCardDto> movies, List<TvCardDto> shows)>[] libraryDataTasks = libraries
            .Select(async library =>
            {
                Task<List<MovieCardDto>> moviesTask = libraryRepository.GetLibraryMovieCardsAsync(userId, library.Id, country, 6, 0, ct);
                Task<List<TvCardDto>> showsTask = libraryRepository.GetLibraryTvCardsAsync(userId, library.Id, country, 6, 0, ct);
                await Task.WhenAll(moviesTask, showsTask);
                return (library, moviesTask.Result, showsTask.Result);
            })
            .ToArray();

        (Library library, List<MovieCardDto> movies, List<TvCardDto> shows)[] libraryDataResults = await Task.WhenAll(libraryDataTasks);

        List<NmCarouselDto<NmCardDto>> list = [];

        foreach ((Library library, List<MovieCardDto> libraryMovies, List<TvCardDto> libraryShows) in libraryDataResults)
        {
            list.Add(new()
            {
                Id = "library_" + library.Id,
                Title = library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                Items = libraryMovies.Select(m => new NmCardDto(m, country))
                    .Concat(libraryShows.Select(t => new NmCardDto(t, country)))
                    .ToList()
            });
        }

        list.Add(new()
        {
            Id = "library_collections",
            Title = "Collections",
            MoreLink = new("/collection", UriKind.Relative),
            Items = collections.Select(collection => new NmCardDto(collection, country))
                .ToList()
        });

        list.Add(new()
        {
            Id = "library_specials",
            Title = "Specials",
            MoreLink = new("/specials", UriKind.Relative),
            Items = specials.Select(special => new NmCardDto(special, country))
                .ToList()
        });

        List<NmCardDto> genres = [];
        if (tv != null)
            genres.Add(new(tv, language));

        if (movie != null)
            genres.Add(new(movie, language));

        List<ComponentEnvelope> components = new();
        
        // Add carousels for each library
        for (int index = 0; index < list.Count; index++)
        {
            NmCarouselDto<NmCardDto> carouselData = list[index];
            dynamic? carousel = Component.Carousel()
                .WithId(carouselData.Id)
                .WithTitle(carouselData.Title)
                .WithMoreLink(carouselData.MoreLink)
                .WithNavigation(
                    index == 0 ? "home_card" : list[index - 1].Id,
                    index == list.Count - 1 ? null : list[index + 1].Id)
                .WithItems(carouselData.Items.Take(6).Select(item => Component.Card()
                    .WithData(new(item))
                    ))
                ;
            components.Add(carousel);
        }
        
        return Ok(ComponentResponse.From(components));
    }

    [HttpGet]
    [Route("{libraryId:ulid}")]
    public async Task<IActionResult> Library(Ulid libraryId, [FromQuery] PageRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view library");

        string language = Language();
        string country = Country();

        // Fetch movies and shows in parallel using optimized projection queries
        Task<List<MovieCardDto>> moviesTask = libraryRepository.GetLibraryMovieCardsAsync(userId, libraryId, country, request.Take, request.Page * request.Take, ct);
        Task<List<TvCardDto>> showsTask = libraryRepository.GetLibraryTvCardsAsync(userId, libraryId, country, request.Take, request.Page * request.Take, ct);

        await Task.WhenAll(moviesTask, showsTask);

        List<MovieCardDto> libraryMovies = moviesTask.Result;
        List<TvCardDto> libraryShows = showsTask.Result;

        if (request.Version != "lolomo")
        {
            List<CardData> cardItems = libraryMovies
                .Select(movie => new CardData(movie, country))
                .Concat(libraryShows.Select(tv => new CardData(tv, country)))
                .OrderBy(item => item.TitleSort)
                .ToList();

            ComponentEnvelope response = Component.Grid()
                .WithId($"library-{libraryId}")
                .WithItems(cardItems.Select(item => Component.Card()
                    .WithData(item)
                ));

            return Ok(ComponentResponse.From(response));
        }
        List<ComponentEnvelope> components = new();

        foreach (string letter in Letters)
        {
            int index = Array.IndexOf(Letters, letter);
            
            List<CardData> carouselItems = libraryMovies
                .Select(movie => new CardData(movie, country))
                .Where(collection => letter == "#"
                    ? Numbers.Any(p => collection.Title.StartsWith(p))
                    : collection.Title.StartsWith(letter))
                .Concat(libraryShows.Select(tv => new CardData(tv, country))
                    .Where(collection => letter == "#"
                        ? Numbers.Any(p => collection.Title.StartsWith(p))
                        : collection.Title.StartsWith(letter)))
                .OrderBy(item => item.TitleSort)
                .ToList();

            if (carouselItems.Count == 0)
                continue;
            
            components.Add(Component.Carousel()
                .WithId(letter)
                .WithTitle(letter)
                .WithNavigation(
                    index == 0 ? null : Letters.ElementAtOrDefault(index - 1) ?? null,
                    index == Letters.Length - 1 ? null : Letters.ElementAtOrDefault(index + 1) ?? null)
                .WithItems(carouselItems.Select(item => Component.Card()
                    .WithData(item)
                )));

        }

        return Ok(new ComponentResponse() { Data = components });
    }

    [HttpGet]
    [Route("{libraryId:ulid}/letter/{letter}")]
    public async Task<IActionResult> LibraryByLetter(Ulid libraryId, string letter, [FromQuery] PageRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view library");

        string language = Language();
        string country = Country();

        // Fetch movies and shows in parallel
        Task<List<Movie>> moviesTask = libraryRepository
            .GetPaginatedLibraryMovies(userId, libraryId, letter, language, country, request.Take, request.Page, ct);
        Task<List<Tv>> showsTask = libraryRepository
            .GetPaginatedLibraryShows(userId, libraryId, letter, language, country, request.Take, request.Page, ct: ct);

        await Task.WhenAll(moviesTask, showsTask);

        List<Movie> movies = moviesTask.Result;
        List<Tv> shows = showsTask.Result;

        List<CardData> concat = movies
            .Select(movie => new CardData(movie, country))
            .Concat(shows.Select(tv => new CardData(tv, country)))
            .OrderBy(item => item.TitleSort)
            .ToList();

        ComponentEnvelope response = Component.Grid()
            .WithId($"library-{libraryId}-{letter}")
            .WithTitle(letter)
            .WithItems(concat.Select(item => Component.Card()
                .WithData(item)
                ))
            ;

        return Ok(ComponentResponse.From(response));
    }
}