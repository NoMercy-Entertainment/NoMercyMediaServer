using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Data.Repositories;
using NoMercy.Helpers.Extensions;

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
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["take", "page"])]
    public async Task<IActionResult> Genres([FromQuery] PageRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        string language = Language();

        // Use optimized query that computes counts in database
        List<GenreWithCountsDto> genreDtos = await _genreRepository
            .GetGenresWithCountsAsync(userId, language, request.Take, request.Page);

        // Create cards for each genre
        List<GenreCardData> genreCards = genreDtos
            .Where(g => g.TotalTvShows > 0 || g.TotalMovies > 0)
            .Select(dto => new GenreCardData(dto))
            .ToList();

        ComponentEnvelope response = Component.Grid()
            .WithId("genres")
            .WithItems(genreCards
                .Select(card => Component.GenreCard()
                    .WithData(card)));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("{genreId}")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["take", "page", "version"])]
    public async Task<IActionResult> Genre(int genreId, [FromQuery] PageRequestDto request, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        string language = Language();
        string country = Country();

        (GenreDetailDto? genreDetail, List<HomeMovieCardDto> movies, List<HomeTvCardDto> tvShows) =
            await _genreRepository.GetGenreCardsAsync(userId, genreId, language, country, request.Take, request.Page, ct);

        if (genreDetail is null || (movies.Count == 0 && tvShows.Count == 0))
            return NotFoundResponse("Genre not found");

        if (request.Version != "lolomo")
        {
            // Simple grid view
            IOrderedEnumerable<CardData> concat = movies
                .Select(movie => new CardData(movie, country))
                .Concat(tvShows
                    .Select(tv => new CardData(tv, country)))
                .OrderBy(card => card.TitleSort);

            ComponentEnvelope response = Component.Grid()
                .WithId("genre-items")
                .WithItems(concat.Select(card => Component.Card().WithData(card)))
                ;

            return Ok(ComponentResponse.From(response));
        }

        // Carousel view organized by first letter
        List<ComponentEnvelope> carousels = Letters
            .Select((letter, index) =>
            {
                List<CardData> carouselItems = movies
                    .Where(movie => letter == "#"
                        ? Numbers.Any(p => movie.Title.StartsWith(p))
                        : movie.Title.StartsWith(letter))
                    .Select(movie => new CardData(movie, country))
                    .Concat(tvShows
                        .Where(tv => letter == "#"
                            ? Numbers.Any(p => tv.Title.StartsWith(p))
                            : tv.Title.StartsWith(letter))
                        .Select(tv => new CardData(tv, country)))
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
                    .Build();
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
