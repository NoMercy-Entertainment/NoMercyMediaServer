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

using System.ComponentModel.DataAnnotations.Schema;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Data.Requests;
using NoMercy.Data.Services;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard.Media;

[ApiController]
[Tags(tags: "Dashboard Specials")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "Moderator")]
[Route(template: "api/v{version:apiVersion}/dashboard/specials", Order = 11)]
public class SpecialsController(
    // TODO: remove mediaContext once LibraryLogic accepts IDbContextFactory instead of MediaContext
    MediaContext mediaContext,
    ISpecialRepository specialRepository,
    IStorageDriver storageDriver,
    IStorageFactory storageFactory,
    ILogger<LibraryLogic> libraryLogicLogger
) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        List<Special> specials = await specialRepository.GetAllSpecialsAdminAsync();

        return Ok(
            value: new SpecialsResponseDto
            {
                Data = specials.Select(selector: special => new SpecialsResponseItemDto(special: special)),
            }
        );
    }

    [HttpPost]
    public async Task<IActionResult> Store()
    {
        Guid userId = User.UserId();

        try
        {
            Special special = await specialRepository.CreateSpecialAsync(userId: userId);

            return Ok(
                value: new StatusResponseDto<Special>
                {
                    Status = "ok",
                    Data = special,
                    Message = "Successfully created a new special.",
                    Args = [],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Something went wrong creating the special");
        }
    }

    [HttpGet]
    [Route(template: "{id:ulid}")]
    public async Task<IActionResult> Show(Ulid id)
    {
        Special? special = await specialRepository.GetSpecialByIdAsync(id: id);

        if (special is null)
            return NotFoundResponse(detail: "Special not found");

        return Ok(
            value: new StatusResponseDto<Special>
            {
                Status = "ok",
                Data = special,
                Message = "Successfully retrieved {0} special.",
                Args = [special.Title.OrEmpty()],
            }
        );
    }

    [HttpPatch]
    [Route(template: "{id:ulid}")]
    public async Task<IActionResult> Update(Ulid id, [FromBody] SpecialUpdateRequest request)
    {
        try
        {
            Special? special = await specialRepository.UpdateSpecialAsync(
                id: id,
                title: request.Title,
                overview: request.Overview,
                poster: request.Poster,
                backdrop: request.Backdrop,
                logo: request.Logo
            );

            if (special is null)
                return NotFoundResponse(detail: "Special not found");

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Successfully updated {0} special.",
                    Args = [special.Title.OrEmpty()],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Something went wrong updating the special");
        }
    }

    [HttpDelete]
    [Route(template: "{id:ulid}")]
    public async Task<IActionResult> Delete(Ulid id)
    {
        try
        {
            Special? special = await specialRepository.DeleteSpecialAsync(id: id);

            if (special is null)
                return NotFoundResponse(detail: "Special not found");

            return Ok(
                value: new StatusResponseDto<string>
                {
                    Status = "ok",
                    Message = "Successfully deleted {0} special.",
                    Args = [special.Title.OrEmpty()],
                }
            );
        }
        catch (Exception)
        {
            return InternalServerErrorResponse(detail: "Something went wrong deleting the special");
        }
    }

    [HttpPatch]
    [Route(template: "sort")]
    public async Task<IActionResult> Sort([FromBody] LibrarySortRequest request)
    {
        List<Special> specials = await specialRepository.GetAllSpecialsSortableAsync();

        if (specials.Count == 0)
            return NotFoundResponse(detail: "No specials exist");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Successfully sorted specials.",
                Args = [],
            }
        );
    }

    [HttpPost]
    [Route(template: "rescan")]
    public async Task<IActionResult> RescanAll()
    {
        List<Special> specialsList = await specialRepository.GetAllSpecialsForRescanAsync();

        if (specialsList.Count == 0)
            return NotFoundResponse(detail: "No specials exist");

        List<string?> titles = [];

        return Ok(
            value: new StatusResponseDto<List<string?>>
            {
                Status = "ok",
                Data = titles,
                Message = "Rescanning all specials.",
            }
        );
    }

    [HttpPost]
    [Route(template: "{id:ulid}/rescan")]
    public async Task<IActionResult> Rescan(Ulid id)
    {
        // BLOCKER: LibraryLogic requires a raw MediaContext until it is refactored
        // to accept IDbContextFactory. Remove mediaContext from the ctor at that point.
        LibraryLogic specialLogic = new(
            id: id,
            mediaContext: mediaContext,
            storageDriver: storageDriver,
            storageFactory: storageFactory,
            logger: libraryLogicLogger
        );

        if (await specialLogic.Process())
            return Ok(
                value: new StatusResponseDto<List<dynamic>>
                {
                    Status = "ok",
                    Data = specialLogic.Titles,
                    Message = "Rescanning {0} special.",
                    Args = [specialLogic.Id],
                }
            );

        return NotFoundResponse(detail: "Special not found");
    }

    [HttpGet]
    [Route(template: "{id:ulid}/items")]
    public async Task<IActionResult> GetItems(Ulid id)
    {
        List<SpecialItem> items = await specialRepository.GetSpecialItemsAdminAsync(id: id);

        List<SpecialItemResponseDto> result = items
            .Select(selector: si =>
            {
                if (si.MovieId is not null && si.Movie is not null)
                    return new()
                    {
                        Id = si.Id.ToString(),
                        Order = si.Order,
                        MediaType = "movie",
                        MediaId = si.Movie.Id,
                        Title = si.Movie.Title,
                        Overview = si.Movie.Overview,
                        Still = null,
                        Poster = si.Movie.Poster,
                        Year = si.Movie.ReleaseDate?.Year,
                        ShowTitle = null,
                        SeasonNumber = null,
                        EpisodeNumber = null,
                        Available = si.Movie.VideoFiles.Count > 0,
                    };

                if (si.EpisodeId is not null && si.Episode is not null)
                    return new SpecialItemResponseDto
                    {
                        Id = si.Id.ToString(),
                        Order = si.Order,
                        MediaType = "episode",
                        MediaId = si.Episode.Id,
                        Title = si.Episode.Title.OrEmpty(),
                        Overview = si.Episode.Overview,
                        Still = si.Episode.Still,
                        Poster = null,
                        Year = si.Episode.AirDate?.Year,
                        ShowTitle = si.Episode.Tv.Title,
                        SeasonNumber = si.Episode.SeasonNumber,
                        EpisodeNumber = si.Episode.EpisodeNumber,
                        Available = si.Episode.VideoFiles.Count > 0,
                    };

                return null;
            })
            .Where(predicate: x => x is not null)
            .ToList()!;

        return Ok(value: result);
    }

    [HttpPatch]
    [Route(template: "{id:ulid}/items")]
    public async Task<IActionResult> UpdateItems(
        Ulid id,
        [FromBody] SpecialItemsUpdateRequest request
    )
    {
        List<SpecialItemReplacement> replacements = request
            .Items.Select(selector: item => new SpecialItemReplacement(
                MediaType: item.MediaType,
                MediaId: item.MediaId,
                Order: item.Order
            ))
            .ToList();

        bool found = await specialRepository.ReplaceSpecialItemsAsync(id: id, items: replacements);

        if (!found)
            return NotFoundResponse(detail: $"Special {id} does not exist.");

        return Ok(
            value: new StatusResponseDto<string>
            {
                Status = "ok",
                Message = "Successfully updated special items.",
            }
        );
    }

    [HttpGet]
    [Route(template: "search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(value: q) || q.Length < 2)
            return Ok(value: Array.Empty<SpecialSearchResultDto>());

        Task<List<Movie>> moviesTask = specialRepository.SearchMoviesAsync(query: q, take: 25, ct: ct);
        Task<List<Episode>> episodesTask = specialRepository.SearchEpisodesAsync(query: q, take: 25, ct: ct);

        await Task.WhenAll(tasks: [moviesTask, episodesTask]);

        List<SpecialSearchResultDto> results =
        [
            .. moviesTask.Result.Select(selector: m => new SpecialSearchResultDto
            {
                Id = m.Id,
                MediaType = "movie",
                Title = m.Title,
                Overview = m.Overview,
                Still = null,
                Poster = m.Poster,
                Year = m.ReleaseDate?.Year,
                ShowTitle = null,
                SeasonNumber = null,
                EpisodeNumber = null,
                Available = m.VideoFiles.Count > 0,
            }),
            .. episodesTask.Result.Select(selector: e => new SpecialSearchResultDto
            {
                Id = e.Id,
                MediaType = "episode",
                Title = e.Title.OrEmpty(),
                Overview = e.Overview,
                Still = e.Still,
                Poster = null,
                Year = e.AirDate?.Year,
                ShowTitle = e.Tv.Title,
                SeasonNumber = e.SeasonNumber,
                EpisodeNumber = e.EpisodeNumber,
                Available = e.VideoFiles.Count > 0,
            }),
        ];

        return Ok(value: results);
    }

    [NotMapped]
    public class SpecialUpdateRequest
    {
        [JsonProperty(propertyName: "id")]
        public Ulid Id { get; set; }

        [JsonProperty(propertyName: "title")]
        public string? Title { get; set; }

        [JsonProperty(propertyName: "overview")]
        public string? Overview { get; set; }

        [JsonProperty(propertyName: "poster")]
        public string? Poster { get; set; }

        [JsonProperty(propertyName: "backdrop")]
        public string? Backdrop { get; set; }

        [JsonProperty(propertyName: "logo")]
        public string? Logo { get; set; }
    }

    [NotMapped]
    public class SpecialItemResponseDto
    {
        [JsonProperty(propertyName: "id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty(propertyName: "order")]
        public int Order { get; set; }

        [JsonProperty(propertyName: "media_type")]
        public string MediaType { get; set; } = string.Empty;

        [JsonProperty(propertyName: "media_id")]
        public int MediaId { get; set; }

        [JsonProperty(propertyName: "title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty(propertyName: "overview")]
        public string? Overview { get; set; }

        [JsonProperty(propertyName: "still")]
        public string? Still { get; set; }

        [JsonProperty(propertyName: "poster")]
        public string? Poster { get; set; }

        [JsonProperty(propertyName: "year")]
        public int? Year { get; set; }

        [JsonProperty(propertyName: "show_title")]
        public string? ShowTitle { get; set; }

        [JsonProperty(propertyName: "season_number")]
        public int? SeasonNumber { get; set; }

        [JsonProperty(propertyName: "episode_number")]
        public int? EpisodeNumber { get; set; }

        [JsonProperty(propertyName: "available")]
        public bool Available { get; set; }
    }

    [NotMapped]
    public class SpecialSearchResultDto
    {
        [JsonProperty(propertyName: "id")]
        public int Id { get; set; }

        [JsonProperty(propertyName: "media_type")]
        public string MediaType { get; set; } = string.Empty;

        [JsonProperty(propertyName: "title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty(propertyName: "overview")]
        public string? Overview { get; set; }

        [JsonProperty(propertyName: "still")]
        public string? Still { get; set; }

        [JsonProperty(propertyName: "poster")]
        public string? Poster { get; set; }

        [JsonProperty(propertyName: "year")]
        public int? Year { get; set; }

        [JsonProperty(propertyName: "show_title")]
        public string? ShowTitle { get; set; }

        [JsonProperty(propertyName: "season_number")]
        public int? SeasonNumber { get; set; }

        [JsonProperty(propertyName: "episode_number")]
        public int? EpisodeNumber { get; set; }

        [JsonProperty(propertyName: "available")]
        public bool Available { get; set; }
    }

    [NotMapped]
    public class SpecialItemsUpdateRequest
    {
        [JsonProperty(propertyName: "items")]
        public List<SpecialItemUpdateDto> Items { get; set; } = [];
    }

    [NotMapped]
    public class SpecialItemUpdateDto
    {
        [JsonProperty(propertyName: "media_type")]
        public string MediaType { get; set; } = string.Empty;

        [JsonProperty(propertyName: "media_id")]
        public int MediaId { get; set; }

        [JsonProperty(propertyName: "order")]
        public int Order { get; set; }
    }
}
