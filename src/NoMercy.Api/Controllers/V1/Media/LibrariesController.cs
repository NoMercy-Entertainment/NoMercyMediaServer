

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Dashboard.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Data.Repositories;
using NoMercy.Database;
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
    HomeRepository homeRepository,
    MediaContext mediaContext,
    SpecialRepository specialRepository)
    : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Libraries()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        List<LibrariesResponseItemDto> response = (await libraryRepository.GetLibraries(userId))
            .Select(library => new LibrariesResponseItemDto(library))
            .ToList();

        return Ok(new LibrariesDto
        {
            Data = response.OrderBy(library => library.Order)
        });
    }

    [HttpGet]
    [Route("mobile")]
    public async Task<IActionResult> Mobile()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        string language = Language();
        string country = Country();

        // Start all independent queries in parallel
        Task<List<Library>> librariesTask = libraryRepository.GetLibraries(userId);
        Task<List<Collection>> collectionsTask = collectionRepository.GetCollectionItems(userId, language, country, 10, 0);
        Task<List<Special>> specialsTask = specialRepository.GetSpecialItems(userId, language, country, 10, 0);
        Task<Tv?> randomTvTask = libraryRepository.GetRandomTvShow(userId, language);
        Task<Movie?> randomMovieTask = libraryRepository.GetRandomMovie(userId, language);

        await Task.WhenAll(librariesTask, collectionsTask, specialsTask, randomTvTask, randomMovieTask);

        List<Library> libraries = librariesTask.Result;
        List<Collection> collections = collectionsTask.Result;
        List<Special> specials = specialsTask.Result;
        Tv? tv = randomTvTask.Result;
        Movie? movie = randomMovieTask.Result;

        // Fetch library data in parallel for all non-music libraries
        Library[] nonMusicLibraries = libraries.Where(lib => lib.Type != "music").ToArray();

        // Each parallel task needs its own MediaContext for thread safety
        Task<(Library library, List<Movie> movies, List<Tv> shows)>[] libraryDataTasks = nonMusicLibraries
            .Select(async library =>
            {
                MediaContext context = new();
                List<Movie> libraryMovies = [];
                await foreach (Movie item in libraryRepository.GetLibraryMovies(context, userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc"))
                {
                    libraryMovies.Add(item);
                }

                List<Tv> libraryShows = [];
                await foreach (Tv item in libraryRepository.GetLibraryShows(context, userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc"))
                {
                    libraryShows.Add(item);
                }

                return (library, libraryMovies, libraryShows);
            })
            .ToArray();

        (Library library, List<Movie> movies, List<Tv> shows)[] libraryDataResults = await Task.WhenAll(libraryDataTasks);

        List<NmCarouselDto<NmCardDto>> list = [];

        foreach ((Library library, List<Movie> libraryMovies, List<Tv> libraryShows) in libraryDataResults)
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
                .WithNavigation(null, list.FirstOrDefault()?.Id)
                .WithUpdate("pageLoad", "/home/card")
                ;
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
            .WithItems(components)
            ;

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("tv")]
    public async Task<IActionResult> Tv()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        string language = Language();
        string country = Country();

        // Start all independent queries in parallel
        Task<List<Library>> librariesTask = libraryRepository.GetLibraries(userId);
        Task<List<Collection>> collectionsTask = collectionRepository.GetCollectionItems(userId, language, country, 6, 0);
        Task<List<Special>> specialsTask = specialRepository.GetSpecialItems(userId, language, country, 6, 0);
        Task<Tv?> randomTvTask = libraryRepository.GetRandomTvShow(userId, language);
        Task<Movie?> randomMovieTask = libraryRepository.GetRandomMovie(userId, language);

        await Task.WhenAll(librariesTask, collectionsTask, specialsTask, randomTvTask, randomMovieTask);

        List<Library> libraries = librariesTask.Result;
        List<Collection> collections = collectionsTask.Result;
        List<Special> specials = specialsTask.Result;
        Tv? tv = randomTvTask.Result;
        Movie? movie = randomMovieTask.Result;

        // Fetch library data in parallel for all libraries - each task needs its own MediaContext for thread safety
        Task<(Library library, List<Movie> movies, List<Tv> shows)>[] libraryDataTasks = libraries
            .Select(async library =>
            {
                MediaContext context = new();
                List<Movie> libraryMovies = [];
                await foreach (Movie item in libraryRepository.GetLibraryMovies(context, userId, library.Id, language, 6, 0, m => m.CreatedAt, "desc"))
                {
                    libraryMovies.Add(item);
                }

                List<Tv> libraryShows = [];
                await foreach (Tv item in libraryRepository.GetLibraryShows(context, userId, library.Id, language, 6, 0, m => m.CreatedAt, "desc"))
                {
                    libraryShows.Add(item);
                }

                return (library, libraryMovies, libraryShows);
            })
            .ToArray();

        (Library library, List<Movie> movies, List<Tv> shows)[] libraryDataResults = await Task.WhenAll(libraryDataTasks);

        List<NmCarouselDto<NmCardDto>> list = [];

        foreach ((Library library, List<Movie> libraryMovies, List<Tv> libraryShows) in libraryDataResults)
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
                .WithNavigation(null, list.FirstOrDefault()?.Id)
                .WithUpdate("pageLoad", "/home/card")
                ;
            components.Add(homeCard);
        }
        
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
        
        ComponentEnvelope response = Component.Container()
            .WithId("tv-libraries")
            .WithItems(components)
            ;

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("{libraryId:ulid}")]
    public async Task<IActionResult> Library(Ulid libraryId, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view library");

        string language = Language();
        string country = Country();

        // Fetch movies and shows in parallel - each task needs its own MediaContext for thread safety
        Task<List<Movie>> moviesTask = Task.Run(async () =>
        {
            MediaContext context = new();
            List<Movie> movies = [];
            await foreach (Movie movie in libraryRepository
                               .GetLibraryMovies(context, userId, libraryId, language, request.Take, request.Page, m => m.CreatedAt, "desc"))
            {
                movies.Add(movie);
            }
            return movies;
        });

        Task<List<Tv>> showsTask = Task.Run(async () =>
        {
            MediaContext context = new();
            List<Tv> shows = [];
            await foreach (Tv tv in libraryRepository
                               .GetLibraryShows(context, userId, libraryId, language, request.Take, request.Page, m => m.CreatedAt, "desc"))
            {
                shows.Add(tv);
            }
            return shows;
        });

        await Task.WhenAll(moviesTask, showsTask);

        List<Movie> libraryMovies = moviesTask.Result;
        List<Tv> libraryShows = showsTask.Result;

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
                    ))
                ;

            return Ok(ComponentResponse.From(response));
        }

        List<ComponentEnvelope> carousels = Letters
            .Select((letter, index) =>
            {
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
                    return null;

                return Component.Carousel()
                    .WithId(letter)
                    .WithTitle(letter)
                    .WithMoreLink($"/libraries/{libraryId}/letter/{letter}")
                    .WithNavigation(
                        index == 0 ? null : Letters.ElementAtOrDefault(index - 1) ?? null,
                        index == Letters.Length - 1 ? null : Letters.ElementAtOrDefault(index + 1) ?? null)
                    .WithItems(carouselItems.Select(item => Component.Card()
                        .WithData(item)
                        ))
                    ;
            })
            .Where(c => c != null)
            .Cast<ComponentEnvelope>()
            .ToList();

        ComponentEnvelope containerResponse = Component.Container()
            .WithId($"library-{libraryId}-letters")
            .WithItems(carousels)
            ;

        return Ok(containerResponse);
    }

    [HttpGet]
    [Route("{libraryId:ulid}/letter/{letter}")]
    public async Task<IActionResult> LibraryByLetter(Ulid libraryId, string letter, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view library");

        string language = Language();
        string country = Country();

        // Fetch movies and shows in parallel
        Task<List<Movie>> moviesTask = libraryRepository
            .GetPaginatedLibraryMovies(userId, libraryId, letter, language, country, request.Take, request.Page);
        Task<List<Tv>> showsTask = libraryRepository
            .GetPaginatedLibraryShows(userId, libraryId, letter, language, country, request.Take, request.Page);

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