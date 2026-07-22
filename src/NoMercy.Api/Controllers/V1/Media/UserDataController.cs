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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercy.Api.Controllers.V1.Music;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Users;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem.Domain;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/userData")]
public class UserDataController(
    IHomeRepository homeRepository,
    IUserDataRepository userDataRepository,
    IEventBus eventBus,
    ILogger<UserDataController> logger
) : BaseController
{
    [HttpGet]
    public IActionResult Index()
    {
        // Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthenticatedResponse(detail: "You do not have permission to view user data");

        return Ok(value: new PlaceholderResponse { Data = [] });
    }

    [HttpGet]
    [Route(template: "continue")]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> ContinueWatching()
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthenticatedResponse(detail: "You do not have permission to view continue watching");

        string language = Language();
        string country = Country();

        HashSet<UserData> continueWatching = await homeRepository.GetContinueWatchingAsync(
            userId: userId,
            language: language,
            country: country
        );

        return Ok(
            value: new CarouselResponseDto<NmCardDto>
            {
                Data = continueWatching
                    .Select(selector: item => new NmCardDto(item: item, country: country))
                    .DistinctBy(keySelector: item => item.Link),
            }
        );
    }

    [HttpDelete]
    [Route(template: "continue")]
    public async Task<IActionResult> RemoveContinue(FavoriteRequest body)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthenticatedResponse(
                detail: "You do not have permission to remove continue watching"
            );

        if (!TryParseFavoriteIds(body: body, intId: out int? intId, ulidId: out Ulid? ulidId))
            return BadRequestResponse(detail: "Invalid id for the requested type");

        List<UserData> userData = await userDataRepository.GetUserDataAsync(
            userId: userId,
            type: body.Type,
            intId: intId,
            ulidId: ulidId
        );

        if (userData.Count == 0)
            return NotFoundResponse(detail: "Item not found");

        logger.LogInformation(message: "{UserData}", args: userData);

        await userDataRepository.HideFromContinueWatchingAsync(userData: userData);

        await eventBus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["continue-watching"] });

        return Ok(value: new StatusResponseDto<string> { Status = "ok", Message = "Item removed" });
    }

    [HttpGet]
    [Route(template: "watched")]
    public async Task<IActionResult> Watched([FromQuery] FavoriteRequest body)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthenticatedResponse(detail: "You do not have permission to view watched");

        if (!TryParseFavoriteIds(body: body, intId: out int? intId, ulidId: out Ulid? ulidId))
            return BadRequestResponse(detail: "Invalid id for the requested type");

        UserData? userData = await userDataRepository.GetUserDataSingleAsync(
            userId: userId,
            type: body.Type,
            intId: intId,
            ulidId: ulidId
        );

        if (userData == null)
            return NotFoundResponse(detail: "Item not found");

        return Ok(
            value: new StatusResponseDto<string> { Status = "ok", Message = "Item marked as watched" }
        );
    }

    [HttpGet]
    [Route(template: "favorites")]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> Favorites(CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthenticatedResponse(detail: "You do not have permission to view favorites");

        string language = Language();
        string country = Country();

        FavoritesData favorites = await homeRepository.GetFavoritesAsync(
            userId: userId,
            language: language,
            country: country,
            ct: ct
        );

        List<NmCardDto> cards =
        [
            .. favorites.Movies.Select(selector: movie => new NmCardDto(movie: movie, country: country)),
            .. favorites.TvShows.Select(selector: tv => new NmCardDto(tv: tv, country: country)),
            .. favorites.Collections.Select(selector: collection => new NmCardDto(collection: collection, country: country)),
            .. favorites.Specials.Select(selector: special => new NmCardDto(special: special, country: country)),
        ];

        return Ok(
            value: new CarouselResponseDto<NmCardDto>
            {
                Data = cards
                    .OrderBy(keySelector: card => card.Title, comparer: StringComparer.OrdinalIgnoreCase)
                    .DistinctBy(keySelector: item => item.Link),
            }
        );
    }

    /// <summary>
    /// Parses <see cref="FavoriteRequest.Id"/> as either an int (movie / tv /
    /// collection) or a Ulid (special) depending on <see cref="FavoriteRequest.Type"/>.
    /// Returns false when the id is malformed for the requested type so the
    /// caller can short-circuit with a 400 instead of throwing FormatException
    /// from inside an EF query.
    /// </summary>
    private static bool TryParseFavoriteIds(FavoriteRequest body, out int? intId, out Ulid? ulidId)
    {
        intId = null;
        ulidId = null;
        if (string.IsNullOrEmpty(value: body.Id))
            return false;

        switch (body.Type)
        {
            case MediaTypes.MovieMediaType:
            case MediaTypes.TvMediaType:
            case MediaTypes.CollectionMediaType:
                if (!int.TryParse(s: body.Id, result: out int parsedInt))
                    return false;
                intId = parsedInt;
                return true;
            case MediaTypes.SpecialMediaType:
                if (!Ulid.TryParse(base32: body.Id, ulid: out Ulid parsedUlid))
                    return false;
                ulidId = parsedUlid;
                return true;
            default:
                return false;
        }
    }
}
