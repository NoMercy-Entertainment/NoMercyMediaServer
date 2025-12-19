

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

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<NmCardDto?>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps((props, _) => props
                        .WithNextId("continue")
                        .WithPreviousId("")
                        .WithData(homeCardItem))
                    .Build(),

                ..list.Select((genre, index) => new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithProps((props, _) => props
                        .WithId($"library_{genre.Id}")
                        .WithPreviousId(index == 0
                            ? "continue"
                            : $"library_{list.ElementAtOrDefault(list.IndexOf(genre) - 1)?.Id}")
                        .WithNextId(index == list.Count - 1
                            ? $"library_{genres.FirstOrDefault()?.Id}"
                            : $"library_{list.ElementAtOrDefault(list.IndexOf(genre) + 1)?.Id}")
                        .WithTitle(genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<NmCardDto>()
                                    .WithComponent("NMCard")
                                    .WithProps((p, _) => p
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
        MediaContext context = new();
        int maximumItemsPerPage = 500;
        string tvMediaType = "tv";
        string movieMediaType = "movie";
        string animeMediaType = "anime";
        
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view libraries");

        string language = Language();
        string country = Country();

        IEnumerable<Library> libraries = await libraryRepository.GetLibraries(userId);

        List<NmCarouselDto<NmCardDto>> list = [];
        
        int animeCount = await homeRepository.GetAnimeCountQuery(context, userId);
        int movieCount = await homeRepository.GetMovieCountQuery(context, userId);
        int tvCount = await homeRepository.GetTvCountQuery(context, userId);


        foreach (Library library in libraries)
        {
            List<Movie> movies =
                libraryRepository.GetLibraryMovies(userId, library.Id, language, 6, 0, m => m.CreatedAt, "desc").ToList();
            List<Tv> shows =
                libraryRepository.GetLibraryShows(userId, library.Id, language, 6, 0, m => m.CreatedAt, "desc").ToList();

            bool shouldPaginate = (library.Type == movieMediaType && movieCount > maximumItemsPerPage)
                                  || (library.Type == tvMediaType && tvCount > maximumItemsPerPage)
                                  || (library.Type == animeMediaType && animeCount > maximumItemsPerPage);
            list.Add(new()
            {
                Id = "library_" + library.Id,
                Title = library.Title,
                MoreLink = shouldPaginate
                    ? new($"/libraries/{library.Id}/letter/A", UriKind.Relative)
                    : new($"/libraries/{library.Id}", UriKind.Relative),
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

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<NmCardDto?>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps((props, _) => props
                        .WithId("home_card")
                        .WithNextId(list.FirstOrDefault()?.Id)
                        .WithPreviousId("")
                        .WithData(homeCardItem))
                    .Build(),

                ..list.Select((library, index) => new ComponentBuilder<NmCardDto>()
                    .WithComponent("NMCarousel")
                    .WithProps((props, _) => props
                        .WithId(library.Id)
                        .WithTitle(library.Title)
                        .WithMoreLink(library.MoreLink)
                        .WithPreviousId(index == 0
                            ? "home_card"
                            : list.ElementAtOrDefault(index - 1)?.Id)
                        .WithNextId(index == list.Count - 1
                            ? $"library_{libraries.FirstOrDefault()?.Id}"
                            : list.ElementAtOrDefault(index + 1)?.Id)
                        // .WithPreviousId(list.ElementAtOrDefault(list.IndexOf(library) - 1)?.Id ?? "home_card")
                        // .WithNextId(list.ElementAtOrDefault(list.IndexOf(library) + 1)?.Id ?? "home_card")
                        .WithItems(
                            library.Items
                                .Take(6)
                                .Select(item =>
                                    new ComponentBuilder<NmCardDto>()
                                        .WithComponent("NMCard")
                                        .WithProps((props, _) => props
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

        List<Movie> movies = libraryRepository
            .GetLibraryMovies(userId, libraryId, language, request.Take, request.Page).ToList();
        List<Tv> shows = libraryRepository
            .GetLibraryShows(userId, libraryId, language, request.Take, request.Page).ToList();

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
                        .WithProps((props, _) => props
                            .WithProperties(new(){})
                            .WithItems(
                                concat.Select(item =>
                                    new ComponentBuilder<LibraryResponseItemDto>()
                                        .WithComponent("NMCard")
                                        .WithProps((props, _) => props
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
                .WithProps((props, _) => props
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
                                .WithProps((props, _) => props
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
                    .WithProps((props, _) => props
                        .WithProperties(new()
                        {
                            { "paddingTop", 16 },
                        })
                        .WithItems(
                            concat.Select(item =>
                                new ComponentBuilder<LibraryResponseItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps((props, _) => props
                                        .WithData(item)
                                        .WithWatch())
                                    .Build())))
                    .Build()
            ]
        });
    }
}