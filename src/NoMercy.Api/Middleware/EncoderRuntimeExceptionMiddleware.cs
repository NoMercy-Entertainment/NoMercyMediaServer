using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NoMercy.Encoder.Errors;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

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

    public EncoderRuntimeExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
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

            Logger.App(
                $"[{traceId}] EncoderRuntimeException [{ex.Shape.Id}]: {ex.Message}",
                LogEventLevel.Warning
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
