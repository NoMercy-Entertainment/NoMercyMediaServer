using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            string traceId = context.TraceIdentifier;
            Logger.App($"[{traceId}] Unhandled exception: {ex}", LogEventLevel.Error);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            ProblemDetails problem = new()
            {
                Type = "/docs/errors/internal-server-error",
                Title = "Internal Server Error.",
                Detail = "An unexpected error occurred.",
                Instance = context.Request.Path,
                Status = StatusCodes.Status500InternalServerError,
                Extensions = { { "traceId", traceId } }
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
