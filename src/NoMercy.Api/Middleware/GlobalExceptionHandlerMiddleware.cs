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
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;

    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug(
                "[{TraceIdentifier}] Request cancelled by client: {Path}",
                context.TraceIdentifier,
                context.Request.Path
            );
        }
        catch (Exception ex) when (IsClientDisconnect(ex, context))
        {
            _logger.LogDebug(
                "[{TraceIdentifier}] Client disconnected mid-request: {Path} ({Name}: {Message})",
                context.TraceIdentifier,
                context.Request.Path,
                ex.GetType().Name,
                ex.Message
            );
        }
        catch (Exception ex)
        {
            string traceId = context.TraceIdentifier;
            _logger.LogError("[{TraceId}] Unhandled exception: {Ex}", traceId, ex);

            // Headers already flushed (e.g. mid-stream of a media response) —
            // there's nothing more we can do without throwing again from
            // inside the catch and corrupting the response. Just log and
            // let Kestrel close the connection.
            if (context.Response.HasStarted)
                return;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            ProblemDetails problem = new()
            {
                Type = "/docs/errors/internal-server-error",
                Title = "Internal Server Error.",
                Detail = "An unexpected error occurred.",
                Instance = context.Request.Path,
                Status = StatusCodes.Status500InternalServerError,
                Extensions = { { "traceId", traceId } },
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static bool IsClientDisconnect(Exception ex, HttpContext context)
    {
        if (context.RequestAborted.IsCancellationRequested)
            return true;

        return ex switch
        {
            BadHttpRequestException => true,
            ConnectionResetException => true,
            IOException io
                when io.Message.Contains("client reset", StringComparison.OrdinalIgnoreCase)
                    || io.Message.Contains(
                        "connection was forcibly closed",
                        StringComparison.OrdinalIgnoreCase
                    )
                    || io.InnerException is ConnectionResetException => true,
            _ => false,
        };
    }
}
