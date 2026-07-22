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
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Api.DTOs.Media.Components;
using NoMercy.Authorization;
using NoMercy.Data.DTOs.Specials;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Specials")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/specials")]
public class SpecialController(ISpecialRepository specialRepository) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] PageRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        string language = Language();
        string country = Country();

        List<SpecialCardDto> specials = await specialRepository.GetSpecialCardsAsync(
            userId: userId,
            language: language,
            take: request.Take,
            page: request.Page,
            ct: ct
        );

        if (request.Version != "lolomo")
        {
            List<CardData> cardItems = specials
                .Select(selector: special => new CardData(dto: special, country: country))
                .ToList();

            ComponentEnvelope response = Component
                .Grid()
                .WithItems(builders: cardItems.Select(selector: item => Component.Card().WithData(data: item)));

            return Ok(value: ComponentResponse.From(component: response));
        }

        List<ComponentEnvelope> carousels = Letters
            .Select(selector: letter =>
            {
                List<CardData> letterItems = specials
                    .Select(selector: special => new CardData(dto: special, country: country))
                    .Where(predicate: item => AlphaBucket.Matches(titleSort: item.TitleSort, bucket: letter))
                    .ToList();

                return Component
                    .Carousel()
                    .WithId(id: letter)
                    .WithTitle(title: letter)
                    .WithItems(builders: letterItems.Select(selector: item => Component.Card().WithData(data: item)))
                    .Build();
            })
            .ToList();

        ComponentEnvelope containerResponse = Component.Container().WithItems(items: carousels);

        return Ok(value: containerResponse);
    }

    [HttpGet]
    [Route(template: "{id:ulid}")]
    public async Task<IActionResult> Show(Ulid id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();

        string country = Country();

        SpecialDetailDto? detail = await specialRepository.GetSpecialDetailAsync(userId: userId, id: id, ct: ct);

        if (detail is null)
            return NotFoundResponse(detail: "Special not found");

        IEnumerable<int> movieIds = detail
            .Items.Where(predicate: item => item.MovieId is not null)
            .Select(selector: item => item.MovieId ?? 0);

        IEnumerable<int> tvIds = detail
            .Items.Where(predicate: item => item.EpisodeId is not null)
            .Select(selector: item => item.TvId)
            .Distinct();

        SpecialItemProjections projections = await specialRepository.GetSpecialItemProjectionsAsync(
            userId: userId,
            movieIds: movieIds,
            tvIds: tvIds,
            country: country,
            ct: ct
        );

        List<SpecialItemsDto> items =
        [
            .. projections.Movies.Select(selector: projection => new SpecialItemsDto(movie: projection)),
            .. projections.Tvs.Select(selector: projection => new SpecialItemsDto(tv: projection)),
        ];

        return Ok(value: new DataResponseDto<SpecialResponseItemDto> { Data = new(detail: detail, items: items) });
    }

    [HttpGet]
    [Route(template: "{id:ulid}/available")]
    public async Task<IActionResult> Available(Ulid id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();

        Special? special = await specialRepository.GetSpecialAvailableAsync(userId: userId, id: id);

        bool hasFiles =
            special is not null
            && (
                special.Items.Select(selector: movie => movie.Movie?.VideoFiles).Any()
                || special.Items.Select(selector: movie => movie.Episode?.VideoFiles).Any()
            );

        if (!hasFiles)
            return NotFoundResponse(detail: "Special not found");

        return Ok(
            value: new StatusResponseDto<AvailableResponseDto>
            {
                Data = new() { Available = true },
                Status = "ok",
                Message = "Special is available",
            }
        );
    }

    [HttpGet]
    [Route(template: "{id:ulid}/watch")]
    public async Task<IActionResult> Watch(Ulid id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();

        string language = Language();
        string country = Country();

        Special? special = await specialRepository.GetSpecialPlaylistAsync(
            userId: userId,
            id: id,
            language: language,
            country: country,
            ct: ct
        );

        if (special is null)
            return NotFoundResponse(detail: "Special not found");

        VideoPlaylistResponseDto[] items = special
            .Items.OrderBy(keySelector: item => item.Order)
            .Select(
                selector: (item, index) =>
                    item.EpisodeId is not null
                        ? new(episode: item.Episode ?? new Episode(), playlistType: "specials", playlistId: id, country: country, index: index)
                        : new VideoPlaylistResponseDto(
                            movie: item.Movie ?? new Movie(),
                            playlistType: "specials",
                            playlistId: id,
                            country: country,
                            index: index
                        )
            )
            .ToArray();

        if (items.Length == 0)
            return NotFoundResponse(detail: "Special not found");

        return Ok(value: items);
    }

    [HttpPost]
    [Route(template: "{id:ulid}/like")]
    public async Task<IActionResult> Like(
        Ulid id,
        [FromBody] LikeRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        Special? special = await specialRepository.LikeSpecialAsync(id: id, userId: userId, like: request.Value, ct: ct);

        if (special is null)
            return NotFoundResponse(detail: "Special not found");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "{0} {1}",
                Args = new object[]
                {
                    special.Title.OrEmpty(),
                    request.Value ? "liked" : "unliked",
                },
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:ulid}/watch-list")]
    public async Task<IActionResult> AddToWatchList(
        Ulid id,
        [FromBody] WatchListRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        bool success = await specialRepository.AddToWatchListAsync(specialId: id, userId: userId, add: request.Add, ct: ct);

        if (!success)
            return UnprocessableEntityResponse(detail: "Special not found");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = request.Add
                    ? "Special added to watch list"
                    : "Special removed from watch list",
            }
        );
    }
}
