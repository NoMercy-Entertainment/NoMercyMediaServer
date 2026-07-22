// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Api.DTOs.Music;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Music;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Music;

[ApiController]
[ApiVersion(version: 1.0)]
[Tags(tags: "Music Genres")]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/music/genres", Order = 4)]
public class GenresController : BaseController
{
    private readonly IGenreRepository _genreRepository;

    public GenresController(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {

        Guid userId = User.UserId();

        List<MusicGenreCardDto> genreCards = await _genreRepository.GetMusicGenreCardsAsync(userId: userId);
        IEnumerable<NmGenreCardDto> allGenres = genreCards
            .Select(selector: genre => new NmGenreCardDto(genre: genre))
            .DistinctBy(keySelector: genre => genre.Title);

        bool isLolomo = string.Equals(
            a: request.Version,
            b: "lolomo",
            comparisonType: StringComparison.OrdinalIgnoreCase
        );

        if (isLolomo)
        {
            List<IGrouping<string, NmGenreCardDto>> groups = allGenres
                .GroupBy(keySelector: g => BucketLetter(name: g.Title))
                .OrderBy(keySelector: g => g.Key == "#" ? "zz" : g.Key)
                .ToList();

            List<ComponentEnvelope> items = [Component.Container()];

            for (int i = 0; i < groups.Count; i++)
            {
                IGrouping<string, NmGenreCardDto> group = groups[index: i];
                string id = $"genres-{group.Key.ToLowerInvariant()}";
                string? prevId = i == 0 ? null : $"genres-{groups[index: i - 1].Key.ToLowerInvariant()}";
                string? nextId =
                    i == groups.Count - 1 ? null : $"genres-{groups[index: i + 1].Key.ToLowerInvariant()}";

                items.Add(
                    item: Component
                        .Carousel()
                        .WithId(id: id)
                        .WithNavigation(previousId: prevId, nextId: nextId)
                        .WithTitle(title: $"Genres: {group.Key}".Localize())
                        .WithItems(builders: group.Select(selector: Component.GenreCard))
                );
            }

            return Ok(value: ComponentResponse.From(components: items));
        }

        ComponentEnvelope response = Component
            .Grid()
            .WithItems(builders: allGenres.Select(selector: Component.GenreCard));

        return Ok(value: ComponentResponse.From(component: response));
    }

    [HttpGet]
    [Route(template: "letter/{letter}")]
    public async Task<IActionResult> LibraryByLetter(
        Ulid libraryId,
        string letter,
        [FromQuery] PageRequestDto request
    )
    {

        Guid userId = User.UserId();

        List<MusicGenreCardDto> genreCards =
            await _genreRepository.GetPaginatedMusicGenreCardsAsync(
                userId: userId,
                letter: letter,
                take: request.Take,
                page: request.Page
            );
        IEnumerable<NmGenreCardDto> genres = genreCards
            .Select(selector: genre => new NmGenreCardDto(genre: genre))
            .DistinctBy(keySelector: genre => genre.Title);

        string displayLetter = letter == "_" ? "#" : letter.ToUpperInvariant();

        bool isLolomo = string.Equals(
            a: request.Version,
            b: "lolomo",
            comparisonType: StringComparison.OrdinalIgnoreCase
        );

        if (isLolomo)
        {
            List<ComponentEnvelope> items =
            [
                Component.Container(),
                Component
                    .Carousel()
                    .WithId(id: $"genres-{letter}")
                    .WithTitle(title: $"Genres: {displayLetter}".Localize())
                    .WithItems(builders: genres.Select(selector: Component.GenreCard)),
            ];

            return Ok(value: ComponentResponse.From(components: items));
        }

        ComponentEnvelope grid = Component
            .Grid()
            .WithId(id: $"genres-{letter}")
            .WithTitle(title: $"Genres: {displayLetter}".Localize())
            .WithItems(builders: genres.Select(selector: Component.GenreCard));

        return Ok(value: ComponentResponse.From(component: grid));
    }

    private static string BucketLetter(string? name)
    {
        if (string.IsNullOrEmpty(value: name))
            return "#";
        char first = char.ToLowerInvariant(c: name[index: 0]);
        return first is >= 'a' and <= 'z' ? first.ToString().ToUpperInvariant() : "#";
    }

    [HttpGet]
    [Route(template: "{id:guid}")]
    public async Task<IActionResult> Show(Guid id)
    {
        Guid userId = User.UserId();

        string language = Language();

        MusicGenre? genre = await _genreRepository.GetMusicGenreAsync(userId: userId, genreId: id);

        if (genre is null)
            return NotFoundResponse(detail: "Albums not found");

        return Ok(value: new GenreResponseDto { Data = new(genre: genre, country: language) });
    }
}
