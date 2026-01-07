

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

        IEnumerable<Library> libraries = await libraryRepository.GetLibraries(userId);

        List<NmCarouselDto<NmCardDto>> list = [];

        foreach (Library library in libraries.Where(lib => lib.Type != "music" ))
        {
            List<Movie> movies =
                libraryRepository.GetLibraryMovies(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc").ToList();
            List<Tv> shows =
                libraryRepository.GetLibraryShows(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc").ToList();
            
            Uri moreLink = library.LibraryMovies.Count + library.LibraryTvs.Count > 500
                ? new($"/libraries/{library.Id}/letter/A", UriKind.Relative)
                : new($"/libraries/{library.Id}", UriKind.Relative);

            list.Add(new()
            {
                Title = library.Title,
                MoreLink = moreLink,
                Items = movies.Select(movie => new NmCardDto(movie, country))
                    .Concat(shows.Select(tv => new NmCardDto(tv, country)))
                    .ToList()
            });
        }

        IEnumerable<Collection> collections =
            collectionRepository.GetCollectionItems(userId, language, 10, 0, m => m.CreatedAt, "desc");
        IEnumerable<Special> specials =
            specialRepository.GetSpecialItems(userId, language, 10, 0, m => m.CreatedAt, "desc");

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

        Tv? tv = await libraryRepository.GetRandomTvShow(userId, language);

        Movie? movie = await libraryRepository.GetRandomMovie(userId, language);

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
                    .WithData(new(item))
                    ))
                ;
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

        IEnumerable<Library> libraries = await libraryRepository.GetLibraries(userId);

        List<NmCarouselDto<NmCardDto>> list = [];
        
        foreach (Library library in libraries)
        {
            List<Movie> movies =
                libraryRepository.GetLibraryMovies(userId, library.Id, language, 6, 0, m => m.CreatedAt, "desc").ToList();
            List<Tv> shows =
                libraryRepository.GetLibraryShows(userId, library.Id, language, 6, 0, m => m.CreatedAt, "desc").ToList();

            list.Add(new()
            {
                Id = "library_" + library.Id,
                Title = library.Title,
                MoreLink =  new($"/libraries/{library.Id}", UriKind.Relative),
                // MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                Items = movies.Select(movie => new NmCardDto(movie, country))
                    .Concat(shows.Select(tv => new NmCardDto(tv, country)))
                    .ToList()
            });
        }

        IEnumerable<Collection> collections =
            collectionRepository.GetCollectionItems(userId, language, 6, 0, m => m.CreatedAt, "desc");
        IEnumerable<Special> specials =
            specialRepository.GetSpecialItems(userId, language, 6, 0, m => m.CreatedAt, "desc");

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

        await using MediaContext mediaContext = new();

        Tv? tv = await libraryRepository.GetRandomTvShow(userId, language);

        Movie? movie = await libraryRepository.GetRandomMovie(userId, language);

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

        List<Movie> movies = libraryRepository
            .GetLibraryMovies(userId, libraryId, language, request.Take, request.Page).ToList();
        List<Tv> shows = libraryRepository
            .GetLibraryShows(userId, libraryId, language, request.Take, request.Page).ToList();

        if (request.Version != "lolomo")
        {
            List<CardData> cardItems = movies
                .Select(movie => new CardData(movie, country))
                .Concat(shows.Select(tv => new CardData(tv, country)))
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
                List<CardData> carouselItems = movies
                    .Select(movie => new CardData(movie, country))
                    .Where(collection => letter == "#"
                        ? Numbers.Any(p => collection.Title.StartsWith(p))
                        : collection.Title.StartsWith(letter))
                    .Concat(shows.Select(tv => new CardData(tv, country))
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

        IEnumerable<Movie> movies = await libraryRepository
            .GetPaginatedLibraryMovies(userId, libraryId, letter, language, request.Take, request.Page);

        IEnumerable<Tv> shows = await libraryRepository
            .GetPaginatedLibraryShows(userId, libraryId, letter, language, request.Take, request.Page);

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