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
using NoMercy.Authorization;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.People;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.People;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media People")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "MediaAccess")]
public class PeopleController(
    IPeopleRepository peopleRepository,
    IPersonMetadataProvider personMetadataProvider,
    IServerConfiguration config
) : BaseController
{
    [HttpGet]
    [Route(template: "api/v{version:apiVersion}/person")] // match themoviedb.org API
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["take", "page"])]
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();

        string language = Language();

        List<PeopleResponseItemDto> people = (
            await peopleRepository.GetPeopleAsync(userId: userId, language: language, take: request.Take, page: request.Page)
        )
            .Select(selector: person => new PeopleResponseItemDto(person: person))
            .ToList();

        return GetPaginatedResponse(data: people, request: request);
    }

    [HttpGet]
    [Route(template: "/api/v{version:apiVersion}/person/{id:int}")] // match themoviedb.org API
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Show(int id)
    {
        string country = Country();

        TmdbPersonAppends? personAppends;
        try
        {
            personAppends = await personMetadataProvider.GetPersonAsync(id: id, ct: default);
        }
        catch (Exception)
        {
            // Mirror Movies/Tv: a transient provider (TMDB) failure degrades to a
            // clean 404 rather than surfacing an unhandled 500.
            return NotFoundResponse(detail: "Person not found");
        }

        if (personAppends is null)
            return NotFoundResponse(detail: "Person not found");

        if (personAppends.Adult && !config.ShowAdultContent)
            return UnauthorizedResponse(
                detail: "Person is adult which is not allowed by the server configuration"
            );

        Person? person = await peopleRepository.GetPersonWithCreditsAsync(id: id);

        return Ok(value: new PersonResponseDto { Data = new(tmdbPersonAppends: personAppends, country: country, person: person) });
    }
}
