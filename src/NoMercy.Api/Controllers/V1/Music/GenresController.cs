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
using NoMercy.NmSystem.Extensions;

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
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {
        return await BuildLetterResponseAsync(letter: "_", request: request);
    }

    [HttpGet]
    [Route("letter/{letter}")]
    public async Task<IActionResult> LibraryByLetter(
        Ulid libraryId,
        string letter,
        [FromQuery] PageRequestDto request
    )
    {
        return await BuildLetterResponseAsync(letter: letter, request: request);
    }

    private async Task<IActionResult> BuildLetterResponseAsync(
        string letter,
        PageRequestDto request
    )
    {
        if (!User.IsAllowed())
            return UnauthorizedResponse("You do not have permission to view genres");

        Guid userId = User.UserId();

        // The "all" marker (`_`) returns one carousel per first-letter bucket in
        // alphabetical order, with the symbol bucket (#) at the end.
        if (letter == "_" || letter == "all")
        {
            List<MusicGenreCardDto> allCards = await _genreRepository.GetMusicGenreCardsAsync(
                userId
            );

            IEnumerable<NmGenreCardDto> allGenres = allCards
                .Select(genre => new NmGenreCardDto(genre))
                .DistinctBy(genre => genre.Title);

            List<IGrouping<string, NmGenreCardDto>> groups = allGenres
                .GroupBy(g => BucketLetter(g.Title))
                .OrderBy(g => g.Key == "#" ? "zz" : g.Key)
                .ToList();

            List<ComponentEnvelope> items = [Component.Container()];

            for (int i = 0; i < groups.Count; i++)
            {
                IGrouping<string, NmGenreCardDto> group = groups[i];
                string id = $"genres-{group.Key.ToLowerInvariant()}";
                string? prevId = i == 0 ? null : $"genres-{groups[i - 1].Key.ToLowerInvariant()}";
                string? nextId =
                    i == groups.Count - 1 ? null : $"genres-{groups[i + 1].Key.ToLowerInvariant()}";

                items.Add(
                    Component
                        .Carousel()
                        .WithId(id)
                        .WithNavigation(prevId, nextId)
                        .WithTitle($"Genres: {group.Key}".Localize())
                        .WithItems(group.Select(Component.GenreCard))
                );
            }

            return Ok(ComponentResponse.From(items));
        }

        List<MusicGenreCardDto> genreCards =
            await _genreRepository.GetPaginatedMusicGenreCardsAsync(
                userId,
                letter,
                request.Take,
                request.Page
            );

        IEnumerable<NmGenreCardDto> genres = genreCards
            .Select(genre => new NmGenreCardDto(genre))
            .DistinctBy(genre => genre.Title);

        string displayLetter = letter.ToUpperInvariant();

        List<ComponentEnvelope> letterItems =
        [
            Component.Container(),
            Component
                .Carousel()
                .WithId($"genres-{letter}")
                .WithTitle($"Genres: {displayLetter}".Localize())
                .WithItems(genres.Select(Component.GenreCard)),
        ];

        return Ok(ComponentResponse.From(letterItems));
    }

    private static string BucketLetter(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "#";
        char first = char.ToLowerInvariant(name[0]);
        return first >= 'a' && first <= 'z' ? first.ToString().ToUpperInvariant() : "#";
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

        return Ok(new GenreResponseDto { Data = new(genre, language) });
    }
}
