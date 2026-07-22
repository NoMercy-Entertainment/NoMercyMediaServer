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
using NoMercy.Api.Controllers.V1.Streaming.Dtos;
using NoMercy.Api.Services;
using NoMercy.Authorization;

namespace NoMercy.Api.Controllers.V1.Streaming;

[ApiController]
[Tags(tags: "Streaming")]
[ApiVersion(version: 1.0)]
[Authorize(Policy = "MediaAccess")]
[Route(template: "api/v{version:apiVersion}/streaming/live")]
public class LiveTranscodeController(ILiveTranscodeService service) : BaseController
{
    [HttpGet(template: "sessions")]
    public IActionResult ListSessions()
    {
        return Ok(value: service.ListSessions());
    }

    [HttpPost(template: "sessions")]
    public async Task<IActionResult> StartSession(
        [FromBody] StartLiveSessionRequest request,
        [FromQuery] string? deviceId = null,
        CancellationToken ct = default
    )
    {
        return MapResult(result: await service.StartSessionAsync(userId: User.UserId(), request: request, deviceId: deviceId, ct: ct));
    }

    [HttpGet(template: "sessions/{sessionId}/master.m3u8")]
    public IActionResult GetMasterPlaylist(string sessionId)
    {
        LiveResult result = service.GetMasterPlaylist(sessionId: sessionId);
        if (result.Kind != LiveResultKind.Ok)
            return MapResult(result: result);

        return Content(content: (string)result.Payload!, contentType: "application/vnd.apple.mpegurl");
    }

    [HttpGet(template: "sessions/{sessionId}/playlist.m3u8")]
    public IActionResult GetPlaylist(string sessionId)
    {
        LiveResult result = service.GetPlaylist(sessionId: sessionId);
        if (result.Kind != LiveResultKind.Ok)
            return MapResult(result: result);

        return Content(content: (string)result.Payload!, contentType: "application/vnd.apple.mpegurl");
    }

    [HttpGet(template: "sessions/{sessionId}/segment/{epoch}/{index:int}.ts")]
    public async Task<IActionResult> GetSegment(
        string sessionId,
        string epoch,
        int index,
        CancellationToken ct = default
    )
    {
        LiveResult result = await service.GetSegmentAsync(sessionId: sessionId, epoch: epoch, index: index, ct: ct);
        if (result.Kind != LiveResultKind.Ok)
            return MapResult(result: result);

        Response.Headers[key: "Accept-Ranges"] = "bytes";
        return File(fileStream: (Stream)result.Payload!, contentType: "video/mp2t", enableRangeProcessing: true);
    }

    [HttpPost(template: "sessions/{sessionId}/position")]
    public IActionResult ReportPosition(string sessionId, [FromBody] ReportPositionRequest request)
    {
        return MapResult(result: service.ReportPosition(sessionId: sessionId, request: request));
    }

    /// <summary>
    /// REST fallback for clients that don't use the SignalR
    /// <c>LiveTranscodeHub.ReportBufferHealth</c> method to report their
    /// download-buffer depth and observed downlink.
    /// </summary>
    [HttpPost(template: "sessions/{sessionId}/buffer-health")]
    public IActionResult ReportBufferHealth(
        string sessionId,
        [FromBody] ReportBufferHealthRequest request
    )
    {
        return MapResult(result: service.ReportBufferHealth(sessionId: sessionId, request: request));
    }

    [HttpPost(template: "sessions/{sessionId}/quality")]
    public async Task<IActionResult> ChangeQuality(
        string sessionId,
        [FromBody] ChangeQualityRequest request,
        CancellationToken ct = default
    )
    {
        return MapResult(result: await service.ChangeQualityAsync(sessionId: sessionId, request: request, ct: ct));
    }

    [HttpPost(template: "sessions/{sessionId}/seek")]
    public async Task<IActionResult> Seek(
        string sessionId,
        [FromBody] SeekRequest request,
        CancellationToken ct = default
    )
    {
        return MapResult(result: await service.SeekAsync(sessionId: sessionId, request: request, ct: ct));
    }

    [HttpDelete(template: "sessions/{sessionId}")]
    public async Task<IActionResult> EndSession(string sessionId)
    {
        await service.EndSessionAsync(sessionId: sessionId, ct: HttpContext.RequestAborted);
        return NoContent();
    }

    private IActionResult MapResult(LiveResult result) =>
        result.Kind switch
        {
            LiveResultKind.Ok => Ok(value: result.Payload),
            LiveResultKind.BadRequest => BadRequestResponse(detail: result.Message!),
            LiveResultKind.NotFound => NotFoundResponse(detail: result.Message!),
            LiveResultKind.Gone => GoneResponse(detail: result.Message!),
            LiveResultKind.ServiceUnavailable => ServiceUnavailableResponse(detail: result.Message!),
            LiveResultKind.InternalError => InternalServerErrorResponse(detail: result.Message!),
            LiveResultKind.EncoderError => StatusCode(
                statusCode: result.EncoderStatusCode,
                value: result.EncoderShape
            ),
            _ => InternalServerErrorResponse(detail: "Unexpected result"),
        };
}
