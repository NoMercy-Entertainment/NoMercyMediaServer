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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Controllers.V1;

/// <summary>
/// Reverse-proxy for a TV's own embedded <c>/v1</c> control server (Ktor, port
/// 7626). A browser served over HTTPS cannot reach <c>http://{lanIp}:7626</c>
/// directly (mixed-content + Private Network Access), so the web player casts
/// by talking to this endpoint over its existing HTTPS/JWT session; the server
/// forwards the request LAN-side to the TV and relays the reply back. This lets
/// the web reuse the exact <c>/v1</c> control surface the Android/KMP app uses
/// natively — <c>launch</c>, <c>session</c> polling, <c>session/transport/*</c>.
/// The caller's own bearer token is forwarded: the TV validates the same
/// Keycloak user token the phone presents and auto-pairs the owner by subject.
/// </summary>
[ApiController]
[ApiVersion(version: 1.0)]
[Authorize]
[Route(template: "api/v{version:apiVersion}/cast")]
public class CastProxyController(
    IDbContextFactory<MediaContext> contextFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<CastProxyController> logger
) : BaseController
{
    private const int TvControlPort = 7626;

    // PATCH is not optional: every selection endpoint on the TV's /v1 surface —
    // subtitle, audio, quality, playlist, volume — is a PATCH. Omitting it made
    // the proxy answer 405 before the request ever reached the TV, so picking a
    // track from the web remote silently did nothing while the POST transport
    // controls (play/pause/seek) worked fine.
    [AcceptVerbs(methods: ["GET", "POST", "PUT", "PATCH", "DELETE"])]
    [Route(template: "{deviceId}/{**path}")]
    public async Task<IActionResult> Proxy(string deviceId, string path)
    {
        Guid userId = User.UserId();
        logger.LogInformation(
            message: "[CastProxy] {Method} device={DeviceId} path=/{Path} user={User}", args: [Request.Method, deviceId, path, userId]
        );

        if (userId == Guid.Empty)
            return UnauthenticatedResponse(detail: "No authenticated user on the cast proxy request");

        await using MediaContext mediaContext = await contextFactory.CreateDbContextAsync();
        Device? tv = await mediaContext
            .Devices.AsNoTracking()
            .FirstOrDefaultAsync(predicate: d =>
                d.DeviceId == deviceId && d.OwnerUserId == userId && d.Type == "tv"
            );

        if (tv is null)
        {
            logger.LogWarning(
                message: "[CastProxy] no owned TV device '{DeviceId}' for user {User}", args: [deviceId, userId]
            );
            return NotFoundResponse(detail: $"No owned TV device '{deviceId}' found");
        }

        if (string.IsNullOrWhiteSpace(value: tv.Ip))
        {
            logger.LogWarning(message: "[CastProxy] TV '{Name}' has no LAN IP", args: tv.Name);
            return ServiceUnavailableResponse(detail: "Target TV has no known LAN address");
        }

        // Transparent passthrough: the caller already includes the TV's API
        // version in the path (e.g. "v1/launch"), so forward it verbatim rather
        // than re-prefixing "/v1/" — doubling it (…/v1/v1/launch) 404s the TV.
        string query = Request.QueryString.HasValue ? Request.QueryString.Value! : string.Empty;
        string targetUrl = $"http://{tv.Ip}:{TvControlPort}/{path}{query}";
        logger.LogInformation(message: "[CastProxy] forwarding to {TargetUrl}", args: targetUrl);

        using HttpRequestMessage forward = new(method: new HttpMethod(method: Request.Method), requestUri: targetUrl);

        if (Request.ContentLength is > 0)
        {
            StreamContent content = new(content: Request.Body);
            if (!string.IsNullOrEmpty(value: Request.ContentType))
                content.Headers.TryAddWithoutValidation(name: "Content-Type", value: Request.ContentType);
            forward.Content = content;
        }

        // Forward the caller's own bearer token verbatim — the TV's /v1 accepts
        // the same Keycloak user token the phone uses; no re-mint needed.
        string? authorization = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(value: authorization))
            forward.Headers.TryAddWithoutValidation(name: "Authorization", value: authorization);

        HttpClient client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(seconds: 10);

        try
        {
            using HttpResponseMessage upstream = await client.SendAsync(
                request: forward,
                completionOption: HttpCompletionOption.ResponseHeadersRead
            );

            byte[] body = await upstream.Content.ReadAsByteArrayAsync();
            logger.LogInformation(
                message: "[CastProxy] TV {Ip} replied {Status} ({Bytes} bytes)", args: [tv.Ip, (int)upstream.StatusCode, body.Length]
            );

            Response.StatusCode = (int)upstream.StatusCode;
            Response.ContentType =
                upstream.Content.Headers.ContentType?.ToString() ?? "application/json";
            await Response.Body.WriteAsync(buffer: body);
            return new EmptyResult();
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning(message: "[CastProxy] TV '{Name}' ({Ip}) timed out", args: [tv.Name, tv.Ip]);
            return GatewayTimeoutResponse(detail: $"TV '{tv.Name}' did not respond in time");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                message: "[CastProxy] could not reach TV '{Name}' ({Ip}): {Message}", args: [tv.Name, tv.Ip, ex.Message]
            );
            return ServiceUnavailableResponse(detail: $"Could not reach TV '{tv.Name}': {ex.Message}");
        }
    }
}
