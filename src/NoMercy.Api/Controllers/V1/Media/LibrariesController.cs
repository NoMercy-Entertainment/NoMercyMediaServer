using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Dashboard.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO;
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
    SpecialRepository specialRepository)
    : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Libraries()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        IEnumerable<Library> libraries = await libraryRepository.GetLibraries(userId);
        List<LibrariesResponseItemDto> response = libraries
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

        foreach (Library library in libraries)
        {
            IEnumerable<Movie> movies =
                await libraryRepository.GetLibraryMovies(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");
            IEnumerable<Tv> shows =
                await libraryRepository.GetLibraryShows(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");

            list.Add(new()
            {
                Title = library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
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

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<NmCardDto?>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps(props => props
                        .WithNextId("continue")
                        .WithPreviousId("")
                        .WithData(homeCardItem))
                    .Build(),

                ..list.Select(genre => new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(list.ElementAtOrDefault(list.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(list.ElementAtOrDefault(list.IndexOf(genre) - 1)?.Id ?? "continue")
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
        });
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
            IEnumerable<Movie> movies =
                await libraryRepository.GetLibraryMovies(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");
            IEnumerable<Tv> shows =
                await libraryRepository.GetLibraryShows(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");

            list.Add(new()
            {
                Title = library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
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

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<NmCardDto?>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps(props => props
                        .WithNextId("continue")
                        .WithPreviousId("")
                        .WithData(homeCardItem))
                    .Build(),

                ..list.Select(genre => new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(list.ElementAtOrDefault(list.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(list.ElementAtOrDefault(list.IndexOf(genre) - 1)?.Id ?? "continue")
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
        });
    }

    [HttpGet]
    [Route("{libraryId:ulid}")]
    public async Task<IActionResult> Library(Ulid libraryId, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view library");

        string language = Language();

        IEnumerable<Movie> movies = await libraryRepository
            .GetLibraryMovies(userId, libraryId, language, request.Take, request.Page);
        IEnumerable<Tv> shows = await libraryRepository
            .GetLibraryShows(userId, libraryId, language, request.Take, request.Page);

        if (request.Version != "lolomo")
        {
            IOrderedEnumerable<LibraryResponseItemDto> concat = movies
                .Select(movie => new LibraryResponseItemDto(movie))
                .Concat(shows.Select(tv => new LibraryResponseItemDto(tv)))
                .OrderBy(item => item.TitleSort);

            return Ok(new Render
            {
                Data =
                [
                    new ComponentBuilder<LibraryResponseItemDto>()
                        .WithComponent("NMGrid")
                        .WithProps(props => props
                            .WithItems(
                                concat.Select(item =>
                                    new ComponentBuilder<LibraryResponseItemDto>()
                                        .WithComponent("NMCard")
                                        .WithProps(cardProps => cardProps
                                            .WithData(item)
                                            .WithWatch())
                                        .Build())))
                        .Build()
                ]
            });
        }

        return Ok(new Render
        {
            Data = Letters.Select(genre => new ComponentBuilder<NmCarouselDto<NmCardDto>>()
                .WithComponent("NMCarousel")
                .WithProps(props => props
                    .WithId(genre)
                    .WithTitle(genre)
                    .WithItems(
                        movies.Select(movie => new LibraryResponseItemDto(movie))
                            .Where(item =>
                                genre == "#"
                                    ? Numbers.Any(p => item.Title.StartsWith(p))
                                    : item.Title.StartsWith(genre))
                            .Concat(shows.Select(tv => new LibraryResponseItemDto(tv))
                                .Where(item =>
                                    genre == "#"
                                        ? Numbers.Any(p => item.Title.StartsWith(p))
                                        : item.Title.StartsWith(genre)))
                            .Select(item => new ComponentBuilder<LibraryResponseItemDto>()
                                .WithComponent("NMCard")
                                .WithProps(cardProps => cardProps
                                    .WithData(item)
                                    .WithWatch())
                                .Build())))
                .Build())
        });
    }

    [HttpGet]
    [Route("{libraryId:ulid}/letter/{letter}")]
    public async Task<IActionResult> LibraryByLetter(Ulid libraryId, string letter, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view library");

        string language = Language();

        IEnumerable<Movie> movies = await libraryRepository
            .GetPaginatedLibraryMovies(userId, libraryId, letter, language, request.Take, request.Page);

        IEnumerable<Tv> shows = await libraryRepository
            .GetPaginatedLibraryShows(userId, libraryId, letter, language, request.Take, request.Page);

        List<LibraryResponseItemDto> concat = movies
            .Select(movie => new LibraryResponseItemDto(movie))
            .Concat(shows.Select(tv => new LibraryResponseItemDto(tv)))
            .OrderBy(item => item.TitleSort)
            .ToList();

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<LibraryResponseItemDto>()
                    .WithComponent("NMGrid")
                    .WithProps(props => props
                        .WithItems(
                            concat.Select(item =>
                                new ComponentBuilder<LibraryResponseItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item)
                                        .WithWatch())
                                    .Build())))
                    .Build()
            ]
        });
    }
}