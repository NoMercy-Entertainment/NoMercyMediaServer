using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;
using NoMercy.Helpers;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags("Media Genres")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/genre")]
public class GenresController : BaseController
{
    private readonly GenreRepository _genreRepository;

    public GenresController(GenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Genres([FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        string language = Language();

        // First get the raw Genre entities from the database
        List<Genre> genreEntities = await _genreRepository
            .GetGenresAsync(userId, language, request.Take, request.Page)
            .ToListAsync();

        // Create cards for each genre
        List<GenreCardData> genreCards = genreEntities
            .Where(g => g.GenreTvShows.Count > 0 || g.GenreMovies.Count > 0)
            .Select(genre => new GenreCardData(genre))
            .ToList();

        ComponentEnvelope response = Component.Grid()
            .WithId("genres")
            .WithItems(genreCards
                .Select(card => Component.GenreCard()
                    .WithData(card)
                    ))
            ;

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("{genreId}")]
    public async Task<IActionResult> Genre(int genreId, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        string language = Language();
        string country = Country();

        Genre genre = await _genreRepository.GetGenreAsync(userId, genreId, language, request.Take, request.Page);

        if (genre.GenreTvShows.Count == 0 && genre.GenreMovies.Count == 0)
            return NotFoundResponse("Genre not found");

        if (request.Version != "lolomo")
        {
            // Simple grid view
            IOrderedEnumerable<CardData> concat = genre.GenreMovies
                .Select(genreMovie => new CardData(genreMovie.Movie, country))
                .Concat(genre.GenreTvShows
                    .Select(genteTv => new CardData(genteTv.Tv, country)))
                .OrderBy(card => card.TitleSort);

            ComponentEnvelope response = Component.Grid()
                .WithId("genre-items")
                .WithItems(concat.Select(card => Component.Card().WithData(card)))
                ;

            return Ok(response);
        }

        // Carousel view organized by first letter
        List<ComponentEnvelope> carousels = Letters
            .Select((letter, index) =>
            {
                List<CardData> carouselItems = genre.GenreMovies
                    .Where(libraryMovie => letter == "#"
                        ? Numbers.Any(p => libraryMovie.Movie.Title.StartsWith(p))
                        : libraryMovie.Movie.Title.StartsWith(letter))
                    .Select(genreMovie => new CardData(genreMovie.Movie, country))
                    .Concat(genre.GenreTvShows
                        .Where(libraryTv => letter == "#"
                            ? Numbers.Any(p => libraryTv.Tv.Title.StartsWith(p))
                            : libraryTv.Tv.Title.StartsWith(letter))
                        .Select(genreTv => new CardData(genreTv.Tv, country)))
                    .OrderBy(card => card.TitleSort)
                    .ToList();

                if (carouselItems.Count == 0)
                    return null;

                return Component.Carousel()
                    .WithId(letter)
                    .WithTitle(letter)
                    .WithNavigation(
                        index == 0 ? null : Letters.ElementAtOrDefault(index - 1) ?? null,
                        index == Letters.Length - 1 ? null : Letters.ElementAtOrDefault(index + 1) ?? null)
                    .WithItems(carouselItems.Select(card => Component.Card().WithData(card)))
                    ;
            })
            .Where(c => c != null)
            .Cast<ComponentEnvelope>()
            .ToList();

        ComponentEnvelope containerResponse = Component.Container()
            .WithId("genre-carousels")
            .WithItems(carousels)
            ;

        return Ok(ComponentResponse.From(containerResponse));
    }
}


// Data = Letters.Select(g => new LoloMoRowDto<NmCardDto>
// {
//     Title = g,
//     Id = g,
//     Items = genre.GenreMovies.Take(request.Take)
//         .Where(libraryMovie => g == "#"
//             ? Numbers.Any(p => libraryMovie.Movie.Title.StartsWith(p))
//             : libraryMovie.Movie.Title.StartsWith(g))
//         .Select(genreMovie => new NmCardDto(genreMovie.Movie, country))
//         .Concat(genre.GenreTvShows.Take(request.Take)
//             .Where(libraryTv => g == "#"
//                 ? Numbers.Any(p => libraryTv.Tv.Title.StartsWith(p))
//                 : libraryTv.Tv.Title.StartsWith(g))
//             .Select(genreTv => new NmCardDto(genreTv.Tv, country)))
//         .OrderBy(libraryResponseDto => libraryResponseDto.TitleSort)
// })