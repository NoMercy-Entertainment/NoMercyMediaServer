using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Api.Controllers.V1.Media.DTO.Components;
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

        List<NmGenreCardDto> genres = (await _genreRepository
                .GetMusicGenresAsync(userId))
            .Select(genre => new NmGenreCardDto(genre))
            .DistinctBy(genre => genre.Title)
            .ToList();

        List<GenreCardData> genreCards = genres
            .Select(item => new GenreCardData(new MusicGenre { Id = item.Id, Name = item.Title }))
            .ToList();

        ComponentEnvelope response = Component.Grid()
            .WithItems(genreCards.Select(item => Component.GenreCard(item)
                ))
            ;

        return Ok(ComponentResponse.From(response));
    }

    [HttpGet]
    [Route("{id:guid}")]
    public IActionResult Show(Guid id)
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        return Ok(new PlaceholderResponse
        {
            Data = []
        });
    }
}