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
using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Media;

namespace NoMercy.Api.Controllers.V1.Media;

[ApiController]
[Tags(tags: "Media Content Segments")]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/content-segments")]
public class ContentSegmentsController(IContentSegmentRepository repository) : BaseController
{
    /// <summary>
    /// Paginated overview of every content segment. Handy for the moderator
    /// dashboard when auditing detector output across the library. Filter by
    /// <c>type</c> (Intro / Outro / Recap / Credits) to narrow.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> List(
        [FromQuery] int pageSize = 100,
        [FromQuery] int pageIndex = 0,
        [FromQuery] ContentSegmentType? type = null
    )
    {
        pageSize = Math.Clamp(value: pageSize, min: 1, max: 500);
        if (pageIndex < 0)
            pageIndex = 0;

        List<ContentSegment> segments = await repository.ListAsync(pageSize: pageSize, pageIndex: pageIndex, filterType: type);
        int total = await repository.GetTotalCountAsync();

        return Ok(
            value: new
            {
                data = segments,
                meta = new
                {
                    total,
                    pageSize,
                    pageIndex,
                    totalPages = (int)Math.Ceiling(a: (double)total / pageSize),
                },
            }
        );
    }

    [HttpGet(template: "episode/{episodeId:int}")]
    [ResponseCache(Duration = 60)]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> GetByEpisode(int episodeId)
    {
        List<ContentSegment> segments = await repository.GetForEpisodeAsync(episodeId: episodeId);
        return Ok(value: new { data = segments });
    }

    [HttpGet(template: "movie/{movieId:int}")]
    [ResponseCache(Duration = 60)]
    [Authorize(Policy = "MediaAccess")]
    public async Task<IActionResult> GetByMovie(int movieId)
    {
        List<ContentSegment> segments = await repository.GetForMovieAsync(movieId: movieId);
        return Ok(value: new { data = segments });
    }

    [HttpPost]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Create([FromBody] CreateContentSegmentRequest request)
    {
        if (request.EndSeconds <= request.StartSeconds)
            return BadRequestResponse(detail: "end_seconds must be greater than start_seconds");

        if (!request.EpisodeId.HasValue && !request.MovieId.HasValue)
            return BadRequestResponse(detail: "Either episode_id or movie_id must be set");

        if (request is { EpisodeId: not null, MovieId: not null })
            return BadRequestResponse(detail: "Provide exactly one of episode_id / movie_id, not both");

        ContentSegment segment = new()
        {
            EpisodeId = request.EpisodeId,
            MovieId = request.MovieId,
            SegmentType = request.SegmentType,
            StartSeconds = request.StartSeconds,
            EndSeconds = request.EndSeconds,
            Source = request.Source ?? "manual",
            Confidence = request.Confidence ?? 1.0,
        };

        ContentSegment saved = await repository.CreateAsync(segment: segment);
        return Ok(value: saved);
    }

    [HttpPut(template: "{id}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateContentSegmentRequest request
    )
    {
        if (!Ulid.TryParse(base32: id, ulid: out Ulid segmentId))
            return BadRequestResponse(detail: "Invalid segment id");

        ContentSegment? updated = await repository.UpdateAsync(
            id: segmentId,
            apply: seg =>
            {
                if (request.SegmentType.HasValue)
                    seg.SegmentType = request.SegmentType.Value;
                if (request.StartSeconds.HasValue)
                    seg.StartSeconds = request.StartSeconds.Value;
                if (request.EndSeconds.HasValue)
                    seg.EndSeconds = request.EndSeconds.Value;
                if (request.Confidence.HasValue)
                    seg.Confidence = request.Confidence.Value;
                // Manual edits flip the source so the next detector run
                // doesn't clobber the user's correction.
                seg.Source = "manual";
            }
        );

        if (updated is null)
            return NotFoundResponse(detail: "Content segment not found");

        return Ok(value: updated);
    }

    [HttpDelete(template: "{id}")]
    [Authorize(Policy = "Moderator")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!Ulid.TryParse(base32: id, ulid: out Ulid segmentId))
            return BadRequestResponse(detail: "Invalid segment id");

        bool deleted = await repository.DeleteAsync(id: segmentId);
        return deleted ? NoContent() : NotFoundResponse(detail: "Content segment not found");
    }
}

public record CreateContentSegmentRequest(
    [property: JsonProperty(propertyName: "segment_type")] ContentSegmentType SegmentType,
    [property: JsonProperty(propertyName: "start_seconds")] double StartSeconds,
    [property: JsonProperty(propertyName: "end_seconds")] double EndSeconds,
    [property: JsonProperty(propertyName: "episode_id")] int? EpisodeId = null,
    [property: JsonProperty(propertyName: "movie_id")] int? MovieId = null,
    [property: JsonProperty(propertyName: "source")] string? Source = null,
    [property: JsonProperty(propertyName: "confidence")] double? Confidence = null
);

public record UpdateContentSegmentRequest(
    [property: JsonProperty(propertyName: "segment_type")] ContentSegmentType? SegmentType = null,
    [property: JsonProperty(propertyName: "start_seconds")] double? StartSeconds = null,
    [property: JsonProperty(propertyName: "end_seconds")] double? EndSeconds = null,
    [property: JsonProperty(propertyName: "confidence")] double? Confidence = null
);
