using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;
using NoMercy.Networking;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags("Media Genres")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/genres")]
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
        request.Take = 1;

        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        string language = Language();

        List<GenresResponseItemDto> genres = await _genreRepository
            .GetGenresAsync(userId, language, request.Take, request.Page)
            .Select(genre => new GenresResponseItemDto(genre))
            .ToListAsync();

        return GetPaginatedResponse(genres, request);
    }

    [HttpGet]
    [Route("{genreId}")]
    public async Task<IActionResult> Genre(int genreId, [FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        string language = Language();

        Genre genre = await _genreRepository.GetGenreAsync(userId, genreId, language, request.Take, request.Page);

        if (genre.GenreTvShows.Count == 0 && genre.GenreMovies.Count == 0)
            return NotFoundResponse("Genre not found");

        if (request.Version != "lolomo")
        {
            IOrderedEnumerable<GenreResponseItemDto> concat = genre.GenreMovies
                .Select(movie => new GenreResponseItemDto(movie))
                .Concat(genre.GenreTvShows
                    .Select(tv => new GenreResponseItemDto(tv)))
                .OrderBy(libraryResponseDto => libraryResponseDto.TitleSort);

            return GetPaginatedResponse(concat, request);
        }

        string[] numbers = ["*", "#", "'", "\"", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"];
        string[] letters =
        [
            "#", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N",
            "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"
        ];

        return Ok(new LoloMoResponseDto<GenreResponseItemDto>
        {
            Data = letters.Select(g => new LoloMoRowDto<GenreResponseItemDto>
            {
                Title = g,
                Id = g,
                Items = genre.GenreMovies.Take(request.Take)
                    .Where(libraryMovie => g == "#"
                        ? numbers.Any(p => libraryMovie.Movie.Title.StartsWith(p))
                        : libraryMovie.Movie.Title.StartsWith(g))
                    .Select(movie => new GenreResponseItemDto(movie))
                    .Concat(genre.GenreTvShows.Take(request.Take)
                        .Where(libraryTv => g == "#"
                            ? numbers.Any(p => libraryTv.Tv.Title.StartsWith(p))
                            : libraryTv.Tv.Title.StartsWith(g))
                        .Select(tv => new GenreResponseItemDto(tv)))
                    .OrderBy(libraryResponseDto => libraryResponseDto.TitleSort)
            })
        });
    }
}