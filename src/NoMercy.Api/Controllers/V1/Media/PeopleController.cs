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
[ApiVersion(1.0)]
[Authorize]
public class PeopleController(
    IPeopleRepository peopleRepository,
    IPersonMetadataProvider personMetadataProvider,
    IServerConfiguration config
) : BaseController
{
    [HttpGet]
    [Route("api/v{version:apiVersion}/person")] // match themoviedb.org API
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["take", "page"])]
    public async Task<IActionResult> Index([FromQuery] PageRequestDto request)
    {
        Guid userId = User.UserId();
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to view people");

        string language = Language();

        List<PeopleResponseItemDto> people = (
            await peopleRepository.GetPeopleAsync(userId, language, request.Take, request.Page)
        )
            .Select(person => new PeopleResponseItemDto(person))
            .ToList();

        return GetPaginatedResponse(people, request);
    }

    [HttpGet]
    [Route("/api/v{version:apiVersion}/person/{id:int}")] // match themoviedb.org API
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Show(int id)
    {
        if (!AuthPolicy.IsAllowed(User))
            return UnauthorizedResponse("You do not have permission to view a person");

        string country = Country();

        TmdbPersonAppends? personAppends = await personMetadataProvider.GetPersonAsync(id, default);

        if (personAppends is null)
            return NotFoundResponse("Person not found");

        if (personAppends.Adult && !config.ShowAdultContent)
            return UnauthorizedResponse(
                "Person is adult which is not allowed by the server configuration"
            );

        Person? person = await peopleRepository.GetPersonWithCreditsAsync(id);

        return Ok(new PersonResponseDto { Data = new(personAppends, country, person) });
    }
}
