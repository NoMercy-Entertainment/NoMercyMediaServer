using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Media;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
using NoMercy.Api.Controllers.V1.Music.DTO;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;
using NoMercy.Helpers;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(1.0)]
[Tags("Music Genres")]
[Authorize]
[Route("api/v{version:apiVersion}/music/genres", Order = 4)]
public class GenresController : BaseController
{
    private readonly GenreRepository _genreRepository;

    public GenresController(GenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }
    
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        Guid userId = User.UserId();

        IEnumerable<NmGenreCardDto> genres = (await _genreRepository.GetMusicGenresAsync(userId))
            .Select(genre => new NmGenreCardDto(genre))
            .DistinctBy(genre => genre.Title);

        ComponentEnvelope response = Component.Grid()
            .WithItems(genres.Select(Component.GenreCard));

        return Ok(ComponentResponse.From(response));
    }
    
    [HttpGet]
    [Route("letter/{letter}")]
    public async Task<IActionResult> LibraryByLetter(Ulid libraryId, string letter, [FromQuery] PageRequestDto request)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        Guid userId = User.UserId();

        IEnumerable<NmGenreCardDto> genres = (await _genreRepository.GetPaginatedMusicGenresAsync(userId, letter, request.Take, request.Page))
            .Select(genre => new NmGenreCardDto(genre))
            .DistinctBy(genre => genre.Title);

        ComponentEnvelope response = Component.Grid()
            .WithItems(genres.Select(Component.GenreCard));

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view albums");

        string language = Language();

        MusicGenre? genre = await _genreRepository.GetMusicGenreAsync(userId, id);

        if (genre is null)
            return NotFoundResponse("Albums not found");

        return Ok(new GenreResponseDto
        {
            Data = new(genre, language)
        });
    }
}