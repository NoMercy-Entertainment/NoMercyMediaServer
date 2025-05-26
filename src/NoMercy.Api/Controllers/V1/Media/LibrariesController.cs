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
    public IActionResult Libraries()
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        IQueryable<Library> libraries = libraryRepository.GetLibraries(userId);

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

        IQueryable<Library> libraries = libraryRepository.GetLibraries(userId);

        List<GenreRowDto<dynamic>> list = [];

        foreach (Library library in libraries)
        {
            IEnumerable<Movie> movies = libraryRepository.GetLibraryMovies(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");
            IEnumerable<Tv> shows = libraryRepository.GetLibraryShows(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");

            list.Add(new()
            {
                Title = library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                Items = movies.Select(movie => new GenreRowItemDto(movie, country))
                    .Concat(shows.Select(tv => new GenreRowItemDto(tv, country)))
            });
        }

        IEnumerable<Collection> collections = collectionRepository.GetCollectionItems(userId, language, 10, 0, m => m.CreatedAt, "desc");
        IEnumerable<Special> specials = specialRepository.GetSpecialItems(userId, language, 10, 0, m => m.CreatedAt, "desc");

        list.Add(new()
        {
            Title = "Collections",
            MoreLink = new("/collection", UriKind.Relative),
            Items = collections.Select(collection => new GenreRowItemDto(collection, country))
        });

        list.Add(new()
        {
            Title = "Specials",
            MoreLink = new("/specials", UriKind.Relative),
            Items = specials.Select(special => new GenreRowItemDto(special, country))
        });

        await using MediaContext mediaContext = new();

        Tv? tv = await Queries.GetRandomTvShow(mediaContext, userId, language);

        Movie? movie = await Queries.GetRandomMovie(mediaContext, userId, language);

        List<GenreRowItemDto> genres = [];
        if (tv != null)
            genres.Add(new(tv, language));

        if (movie != null)
            genres.Add(new(movie, language));

        GenreRowItemDto? homeCardItem = genres.Where(g => !string.IsNullOrWhiteSpace(g.Title))
            .Randomize().FirstOrDefault();

        return Ok(new Render
        {
            Data = [
                new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps(props => props
                        .WithNextId("continue")
                        .WithPreviousId("")
                        .WithData(homeCardItem))
                    .Build(),
                
                ..list.Select(genre => new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(list.ElementAtOrDefault(list.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(list.ElementAtOrDefault(list.IndexOf(genre) - 1)?.Id ?? "continue")
                        .WithTitle(genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<GenreRowItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item ?? new GenreRowItemDto())
                                        .WithWatch())
                                    .Build())))
                    .Build()),
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

        IQueryable<Library> libraries = libraryRepository.GetLibraries(userId);

        List<GenreRowDto<dynamic>> list = [];

        foreach (Library library in libraries)
        {
            IEnumerable<Movie> movies = libraryRepository.GetLibraryMovies(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");
            IEnumerable<Tv> shows = libraryRepository.GetLibraryShows(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");

            list.Add(new()
            {
                Title = library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                Items = movies.Select(movie => new GenreRowItemDto(movie, country))
                    .Concat(shows.Select(tv => new GenreRowItemDto(tv, country)))
            });
        }

        IEnumerable<Collection> collections = collectionRepository.GetCollectionItems(userId, language, 10, 0, m => m.CreatedAt, "desc");
        IEnumerable<Special> specials = specialRepository.GetSpecialItems(userId, language, 10, 0, m => m.CreatedAt, "desc");

        list.Add(new()
        {
            Title = "Collections",
            MoreLink = new("/collection", UriKind.Relative),
            Items = collections.Select(collection => new GenreRowItemDto(collection, country))
        });

        list.Add(new()
        {
            Title = "Specials",
            MoreLink = new("/specials", UriKind.Relative),
            Items = specials.Select(special => new GenreRowItemDto(special, country))
        });

        await using MediaContext mediaContext = new();

        Tv? tv = await Queries.GetRandomTvShow(mediaContext, userId, language);

        Movie? movie = await Queries.GetRandomMovie(mediaContext, userId, language);

        List<GenreRowItemDto> genres = [];
        if (tv != null)
            genres.Add(new(tv, language));

        if (movie != null)
            genres.Add(new(movie, language));

        GenreRowItemDto? homeCardItem = genres.Where(g => !string.IsNullOrWhiteSpace(g.Title))
            .Randomize().FirstOrDefault();

        return Ok(new Render
        {
            Data = [
                new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps(props => props
                        .WithNextId("continue")
                        .WithPreviousId("")
                        .WithData(homeCardItem))
                    .Build(),

                ..list.Select(genre => new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(list.ElementAtOrDefault(list.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(list.ElementAtOrDefault(list.IndexOf(genre) - 1)?.Id ?? "continue")
                        .WithTitle(genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<GenreRowItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item ?? new GenreRowItemDto())
                                        .WithWatch())
                                    .Build())))
                    .Build()),
            ]
        });
    }

    [HttpGet]
    [Route("{libraryId:ulid}")]
    public IActionResult Library(Ulid libraryId, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view library");

        string language = Language();

        IEnumerable<Movie> movies = libraryRepository
            .GetLibraryMovies(userId, libraryId, language, request.Take, request.Page);
        IEnumerable<Tv> shows = libraryRepository
            .GetLibraryShows(userId, libraryId, language, request.Take, request.Page);

        if (request.Version != "lolomo")
        {
            IOrderedEnumerable<LibraryResponseItemDto> concat = movies
                .Select(movie => new LibraryResponseItemDto(movie))
                .Concat(shows.Select(tv => new LibraryResponseItemDto(tv)))
                .OrderBy(item => item.TitleSort);

            return GetPaginatedResponse(concat, request);
        }

        string[] numbers = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];
        string[] letters =
        [
            "#", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T",
            "U", "V", "W", "X", "Y", "Z"
        ];
        
        return Ok(new Render
        {
            Data = letters.Select(genre => new ComponentBuilder<LibraryResponseItemDto>()
                .WithComponent("NMCarousel")
                .WithProps(props => props
                    .WithId(genre)
                    .WithTitle(genre)
                    .WithItems(
                        movies.Select(movie => new LibraryResponseItemDto(movie))
                            .Where(item => genre == "#" ? numbers.Any(p => item.Title.StartsWith(p)) : item.Title.StartsWith(genre))
                            .Concat(shows.Select(tv => new LibraryResponseItemDto(tv))
                                .Where(item => genre == "#" ? numbers.Any(p => item.Title.StartsWith(p)) : item.Title.StartsWith(genre)))
                            .Select(item => new ComponentBuilder<LibraryResponseItemDto>()
                                .WithComponent("NMCard")
                                .WithProps(cardProps => cardProps
                                    .WithData(item)
                                    .WithWatch())
                                .Build())))
                .Build())
        });
    }
}
