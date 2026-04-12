using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoMercy.NmSystem.Extensions;
using NoMercy.Setup;

namespace NoMercy.Api.Middleware;

public class SetupModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SetupState _setupState;
    private readonly SetupEndpoints _setupEndpoints;

    private static readonly string[] SetupHandledRoutes =
    [
        "/setup",
        "/sso-callback",
        "/favicon.ico",
    ];

    private static readonly string[] PassthroughRoutes = ["/health", "/manage"];

    public SetupModeMiddleware(
        RequestDelegate next,
        SetupState setupState,
        SetupEndpoints setupEndpoints
    )
    {
        _next = next;
        _setupState = setupState;
        _setupEndpoints = setupEndpoints;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string path = (context.Request.Path.Value?.TrimEnd('/')).OrEmpty();
        string pathLower = path.ToLowerInvariant();

        // Always serve setup routes (even after Complete) — the setup page
        // JS shows the success screen and redirects to the HTTPS URL.
        foreach (string route in SetupHandledRoutes)
        {
            if (pathLower == route || pathLower.StartsWith(route + "/"))
            {
                await _setupEndpoints.HandleRequestAsync(context);
                return;
            }
        }

        if (!_setupState.IsSetupRequired)
        {
            await _next(context);
            return;
        }

        foreach (string route in SetupHandledRoutes)
        {
            if (pathLower == route || pathLower.StartsWith(route + "/"))
            {
                await _setupEndpoints.HandleRequestAsync(context);
                return;
            }
        }

        foreach (string route in PassthroughRoutes)
        {
            if (pathLower == route || pathLower.StartsWith(route + "/"))
            {
                await _next(context);
                return;
            }
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        object response = new
        {
            status = "setup_required",
            message = "Server is in setup mode",
            setup_url = "/setup",
        };

        string json = JsonConvert.SerializeObject(response, Formatting.Indented);
        await context.Response.WriteAsync(json, Encoding.UTF8);
    }
}
