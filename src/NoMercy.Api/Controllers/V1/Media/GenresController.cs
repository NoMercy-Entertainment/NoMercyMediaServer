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
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Genres")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/genres")]
public class GenresController : BaseController
{
    private readonly IGenreRepository _genreRepository;

    public GenresController(IGenreRepository genreRepository)
    {
        _genreRepository = genreRepository;
    }

    [HttpGet]
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["take", "page"])]
    public async Task<IActionResult> Genres(
        [FromQuery] PageRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        string language = Language();

        // Use optimized query that computes counts in database
        List<GenreWithCountsDto> genreDtos = await _genreRepository.GetGenresWithCountsAsync(
            userId: userId,
            language: language,
            take: request.Take,
            page: request.Page
        );

        // Create cards for each genre
        List<GenreCardData> genreCards = genreDtos
            .Where(predicate: g => g.TotalTvShows > 0 || g.TotalMovies > 0)
            .Select(selector: dto => new GenreCardData(dto: dto))
            .ToList();

        ComponentEnvelope response = Component
            .Grid()
            .WithId(id: "genres")
            .WithItems(builders: genreCards.Select(selector: card => Component.GenreCard().WithData(data: card)));

        return Ok(value: ComponentResponse.From(component: response));
    }

    [HttpGet]
    [Route(template: "{genreId}")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["take", "page", "version"])]
    public async Task<IActionResult> Genre(
        int genreId,
        [FromQuery] PageRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        string language = Language();
        string country = Country();

        (GenreDetailDto? genreDetail, List<HomeMovieCardDto> movies, List<HomeTvCardDto> tvShows) =
            await _genreRepository.GetGenreCardsAsync(
                userId: userId,
                id: genreId,
                language: language,
                country: country,
                take: request.Take,
                page: request.Page,
                ct: ct
            );

        if (genreDetail is null || (movies.Count == 0 && tvShows.Count == 0))
            return NotFoundResponse(detail: "Genre not found");

        if (request.Version != "lolomo")
        {
            // Simple grid view
            IOrderedEnumerable<CardData> concat = movies
                .Select(selector: movie => new CardData(movie: movie, country: country))
                .Concat(second: tvShows.Select(selector: tv => new CardData(tv: tv, country: country)))
                .OrderBy(keySelector: card => card.TitleSort);

            ComponentEnvelope response = Component
                .Grid()
                .WithId(id: "genre-items")
                .WithItems(builders: concat.Select(selector: card => Component.Card().WithData(data: card)));

            return Ok(value: ComponentResponse.From(component: response));
        }

        // Carousel view organized by first letter
        List<ComponentEnvelope> carousels = Letters
            .Select(
                selector: (letter, index) =>
                {
                    List<CardData> carouselItems = movies
                        .Select(selector: movie => new CardData(movie: movie, country: country))
                        .Where(predicate: card => AlphaBucket.Matches(titleSort: card.TitleSort, bucket: letter))
                        .Concat(
                            second: tvShows
                                .Select(selector: tv => new CardData(tv: tv, country: country))
                                .Where(predicate: card => AlphaBucket.Matches(titleSort: card.TitleSort, bucket: letter))
                        )
                        .OrderBy(keySelector: card => card.TitleSort)
                        .ToList();

                    if (carouselItems.Count == 0)
                        return null;

                    return Component
                        .Carousel()
                        .WithId(id: letter)
                        .WithTitle(title: letter)
                        .WithNavigation(
                            previousId: index == 0 ? null : Letters.ElementAtOrDefault(index: index - 1) ?? null,
                            nextId: index == Letters.Length - 1
                                ? null
                                : Letters.ElementAtOrDefault(index: index + 1) ?? null
                        )
                        .WithItems(builders: carouselItems.Select(selector: card => Component.Card().WithData(data: card)))
                        .Build();
                }
            )
            .Where(predicate: c => c != null)
            .Cast<ComponentEnvelope>()
            .ToList();

        ComponentEnvelope containerResponse = Component
            .Container()
            .WithId(id: "genre-carousels")
            .WithItems(items: carousels);

        return Ok(value: ComponentResponse.From(component: containerResponse));
    }
}
