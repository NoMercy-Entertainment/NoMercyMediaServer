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
using NoMercy.Api.DTOs.Playlists;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Playlists;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Playlists;

/// <summary>
/// User-created, ordered, VIDEO-ONLY playlists (movies + tv shows + episodes +
/// specials in one list — never music tracks). Deliberately routed at the
/// top-level "api/v{version}/playlists" — NOT under "/music/" — so it never
/// collides with NoMercy.Api.Controllers.V1.Music.PlaylistsController, the
/// separate music-only Playlist/PlaylistTrack feature this controller never
/// touches. The two features share no table, so a music playlist can never
/// appear in this controller's responses and vice versa. Every action is
/// scoped to the caller's own playlists via IUserPlaylistRepository's
/// ownership-checked methods.
/// </summary>
[ApiController]
[ApiVersion(version: 1.0)]
[Tags(tags: "Playlists")]
[Authorize]
[Route(template: "api/v{version:apiVersion}/playlists")]
public class UserPlaylistsController(IUserPlaylistRepository userPlaylistRepository)
    : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view playlists");

        List<UserPlaylistSummary> playlists = await userPlaylistRepository.GetUserPlaylistsAsync(
            userId: userId,
            ct: ct
        );

        return Ok(
            value: new CarouselResponseDto<UserPlaylistSummaryDto>
            {
                Data = playlists.Select(selector: p => new UserPlaylistSummaryDto(summary: p)),
            }
        );
    }

    [HttpGet]
    [Route(template: "{id:guid}")]
    public async Task<IActionResult> Show(Guid id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(principal: User))
            return UnauthorizedResponse(detail: "You do not have permission to view playlists");

        UserPlaylistDetail? playlist = await userPlaylistRepository.GetPlaylistAsync(
            playlistId: id,
            userId: userId,
            ct: ct
        );
        if (playlist is null)
            return NotFoundResponse(detail: "Playlist not found");

        string language = Language();
        string country = Country();

        List<PlaylistItem>? items = await userPlaylistRepository.GetPlaylistItemsAsync(
            playlistId: id,
            userId: userId,
            language: language,
            country: country,
            ct: ct
        );

        return Ok(
            value: new DataResponseDto<UserPlaylistDetailDto>
            {
                Data = new()
                {
                    Id = playlist.Id,
                    Name = playlist.Name,
                    Description = playlist.Description,
                    Cover = playlist.Cover,
                    Items = (items ?? []).Select(selector: PlaylistItemCardDto.From).ToList(),
                },
            }
        );
    }

    [HttpPost]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserPlaylistRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        if (string.IsNullOrWhiteSpace(value: request.Name))
            return BadRequestResponse(detail: "Name is required");

        Guid id = await userPlaylistRepository.CreatePlaylistAsync(
            userId: userId,
            name: request.Name,
            description: request.Description,
            cover: request.Cover,
            ct: ct
        );

        return Ok(
            value: new StatusResponseDto<UserPlaylistSummaryDto>
            {
                Status = "ok",
                Data = new()
                {
                    Id = id,
                    Name = request.Name,
                    Cover = request.Cover,
                    ItemCount = 0,
                },
            }
        );
    }

    [HttpPatch]
    [Route(template: "{id:guid}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromBody] UpdateUserPlaylistRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        bool updated = await userPlaylistRepository.UpdatePlaylistAsync(
            playlistId: id,
            userId: userId,
            name: request.Name,
            description: request.Description,
            cover: request.Cover,
            ct: ct
        );

        if (!updated)
            return NotFoundResponse(detail: "Playlist not found");

        return Ok(
            value: new StatusResponseDto<string> { Status = "ok", Data = "Playlist updated".Localize() }
        );
    }

    [HttpDelete]
    [Route(template: "{id:guid}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Destroy(Guid id, CancellationToken ct = default)
    {
        Guid userId = User.UserId();

        bool deleted = await userPlaylistRepository.DeletePlaylistAsync(playlistId: id, userId: userId, ct: ct);

        if (!deleted)
            return NotFoundResponse(detail: "Playlist not found");

        return Ok(
            value: new StatusResponseDto<string> { Status = "ok", Data = "Playlist deleted".Localize() }
        );
    }

    [HttpPost]
    [Route(template: "{id:guid}/items")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> AddItem(
        Guid id,
        [FromBody] AddPlaylistItemRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        if (!await userPlaylistRepository.OwnsPlaylistAsync(playlistId: id, userId: userId, ct: ct))
            return NotFoundResponse(detail: "Playlist not found");

        if (!PlaylistItemKindWire.TryParse(value: request.Kind, kind: out PlaylistItemKind kind))
            return BadRequestResponse(detail: "Invalid kind. Expected one of: movie, tv, episode, special");

        PlaylistItemRef? itemRef = BuildItemRef(kind: kind, mediaId: request.MediaId);
        if (itemRef is null)
            return BadRequestResponse(detail: "Invalid media_id for the given kind");

        PlaylistItem? created = await userPlaylistRepository.AddItemAsync(
            playlistId: id,
            userId: userId,
            item: itemRef.Value,
            order: request.Order,
            ct: ct
        );

        // Ownership was already confirmed above, so a null result here
        // unambiguously means the referenced media doesn't exist.
        if (created is null)
            return NotFoundResponse(detail: "Referenced media was not found");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Data = "Item added to playlist".Localize(),
            }
        );
    }

    [HttpDelete]
    [Route(template: "{id:guid}/items/{itemId:ulid}")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> RemoveItem(
        Guid id,
        Ulid itemId,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        bool removed = await userPlaylistRepository.RemoveItemAsync(playlistId: id, userId: userId, itemId: itemId, ct: ct);

        if (!removed)
            return NotFoundResponse(detail: "Item not found in playlist");

        return Ok(
            value: new StatusResponseDto<string> { Status = "ok", Data = "Item removed".Localize() }
        );
    }

    [HttpPut]
    [Route(template: "{id:guid}/items/order")]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> Reorder(
        Guid id,
        [FromBody] ReorderPlaylistItemsRequestDto request,
        CancellationToken ct = default
    )
    {
        Guid userId = User.UserId();

        if (!await userPlaylistRepository.OwnsPlaylistAsync(playlistId: id, userId: userId, ct: ct))
            return NotFoundResponse(detail: "Playlist not found");

        List<Ulid> orderedIds = new(capacity: request.OrderedItemIds.Count);
        foreach (string rawId in request.OrderedItemIds)
        {
            if (!Ulid.TryParse(base32: rawId, ulid: out Ulid parsed))
                return BadRequestResponse(detail: "Invalid item id in ordered_item_ids");
            orderedIds.Add(item: parsed);
        }

        bool reordered = await userPlaylistRepository.ReorderAsync(playlistId: id, userId: userId, orderedItemIds: orderedIds, ct: ct);

        if (!reordered)
            return BadRequestResponse(
                detail: "ordered_item_ids must match the playlist's current items exactly"
            );

        return Ok(
            value: new StatusResponseDto<string> { Status = "ok", Data = "Playlist reordered".Localize() }
        );
    }

    /// <summary>
    /// Parses <paramref name="mediaId"/> to the id type <paramref name="kind"/>
    /// expects (int for movie/tv/episode, Ulid for special). Returns null on a
    /// type mismatch so the caller can 400 instead of 500.
    /// </summary>
    private static PlaylistItemRef? BuildItemRef(PlaylistItemKind kind, string mediaId)
    {
        switch (kind)
        {
            case PlaylistItemKind.Movie:
                return int.TryParse(s: mediaId, result: out int movieId)
                    ? PlaylistItemRef.ForMovie(movieId: movieId)
                    : null;
            case PlaylistItemKind.Tv:
                return int.TryParse(s: mediaId, result: out int tvId) ? PlaylistItemRef.ForTv(tvId: tvId) : null;
            case PlaylistItemKind.Episode:
                return int.TryParse(s: mediaId, result: out int episodeId)
                    ? PlaylistItemRef.ForEpisode(episodeId: episodeId)
                    : null;
            case PlaylistItemKind.Special:
                return Ulid.TryParse(base32: mediaId, ulid: out Ulid specialId)
                    ? PlaylistItemRef.ForSpecial(specialId: specialId)
                    : null;
            default:
                return null;
        }
    }
}
