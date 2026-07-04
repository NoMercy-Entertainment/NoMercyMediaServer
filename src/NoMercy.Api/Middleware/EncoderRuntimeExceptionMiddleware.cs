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

using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NoMercy.Encoder.Errors;

namespace NoMercy.Api.Middleware;

/// <summary>
/// Catches <see cref="EncoderRuntimeException"/> thrown from controllers,
/// SignalR hubs, or jobs, serialises the <see cref="EncoderErrorShape"/> as
/// snake_case JSON, and returns the status code pinned by the factory that
/// created the exception.
///
/// All other exceptions propagate to the next handler unchanged.
/// </summary>
public class EncoderRuntimeExceptionMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy(),
        },
        NullValueHandling = NullValueHandling.Ignore,
    };

    private readonly ILogger<EncoderRuntimeExceptionMiddleware> _logger;

    public EncoderRuntimeExceptionMiddleware(
        RequestDelegate next,
        ILogger<EncoderRuntimeExceptionMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (EncoderRuntimeException ex)
        {
            string traceId = context.TraceIdentifier;

            _logger.LogWarning(
                "[{TraceId}] EncoderRuntimeException [{Id}]: {Message}",
                traceId,
                ex.Shape.Id,
                ex.Message
            );

            // Response already in flight — can't safely overwrite headers.
            // Let the exception bubble to GlobalExceptionHandlerMiddleware,
            // which will also short-circuit and just close the connection.
            if (context.Response.HasStarted)
                throw;

            context.Response.StatusCode = ex.HttpStatusCode;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            string json = JsonConvert.SerializeObject(ex.Shape, SerializerSettings);
            await context.Response.WriteAsync(json);
        }
    }
}
