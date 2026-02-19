using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Music;
using NoMercy.Helpers.Extensions;

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

        List<MusicGenreCardDto> genreCards = await _genreRepository.GetMusicGenreCardsAsync(userId);
        IEnumerable<NmGenreCardDto> genres = genreCards
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

        List<MusicGenreCardDto> genreCards = await _genreRepository.GetPaginatedMusicGenreCardsAsync(userId, letter, request.Take, request.Page);
        IEnumerable<NmGenreCardDto> genres = genreCards
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