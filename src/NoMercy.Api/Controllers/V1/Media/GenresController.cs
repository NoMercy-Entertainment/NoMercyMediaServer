using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Media.DTO;
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

        // Then apply DTO transformation and filtering on the client side
        List<NmGenreCardDto> genres = genreEntities
            .Select(genre => new NmGenreCardDto(genre))
            .Where(g => g.HaveItems > 0)
            .ToList();

        return Ok(new Render
        {
            Data =
            [
                new ComponentBuilder<NmGenreCardDto>()
                    .WithComponent("NMGrid")
                    .WithProps((props, _) => props
                        .WithProperties(new(){})
                        .WithItems(
                            genres
                                .Select(item =>
                                    new ComponentBuilder<NmGenreCardDto>()
                                        .WithComponent("NMGenreCard")
                                        .WithProps((props, _) => props
                                            .WithData(item)
                                            .WithWatch())
                                        .Build())))
                    .Build()
            ]
        });
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
            IOrderedEnumerable<NmCardDto> concat = genre.GenreMovies
                .Select(genreMovie => new NmCardDto(genreMovie.Movie, country))
                .Concat(genre.GenreTvShows
                    .Select(genteTv => new NmCardDto(genteTv.Tv, country)))
                .OrderBy(libraryResponseDto => libraryResponseDto.TitleSort);

            return Ok(new Render
            {
                Data =
                [
                    new ComponentBuilder<NmCardDto>()
                        .WithComponent("NMGrid")
                        .WithProps((props, _) => props
                            .WithProperties(new()
                            {
                                { "paddingTop", 16 },
                            })
                            .WithItems(
                                concat.Select(item =>
                                    new ComponentBuilder<NmCardDto>()
                                        .WithComponent("NMCard")
                                        .WithProps((props, _) => props
                                            .WithData(item)
                                            .WithWatch())
                                        .Build())))
                        .Build()
                ]
            });
        }

        return Ok(new LoloMoResponseDto<NmCardDto>
        {
            Data = Letters.Select(g => new LoloMoRowDto<NmCardDto>
            {
                Title = g,
                Id = g,
                Items = genre.GenreMovies.Take(request.Take)
                    .Where(libraryMovie => g == "#"
                        ? Numbers.Any(p => libraryMovie.Movie.Title.StartsWith(p))
                        : libraryMovie.Movie.Title.StartsWith(g))
                    .Select(genreMovie => new NmCardDto(genreMovie.Movie, country))
                    .Concat(genre.GenreTvShows.Take(request.Take)
                        .Where(libraryTv => g == "#"
                            ? Numbers.Any(p => libraryTv.Tv.Title.StartsWith(p))
                            : libraryTv.Tv.Title.StartsWith(g))
                        .Select(genreTv => new NmCardDto(genreTv.Tv, country)))
                    .OrderBy(libraryResponseDto => libraryResponseDto.TitleSort)
            })
        });
    }
}