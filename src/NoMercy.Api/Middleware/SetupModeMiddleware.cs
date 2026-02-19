using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoMercy.Setup;

namespace NoMercy.Api.Middleware;

public class SetupModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SetupState _setupState;
    private readonly SetupServer _setupServer;

    private static readonly string[] SetupHandledRoutes =
    [
        "/setup",
        "/sso-callback"
    ];

    private static readonly string[] PassthroughRoutes =
    [
        "/health",
        "/manage"
    ];

    public SetupModeMiddleware(RequestDelegate next, SetupState setupState, SetupServer setupServer)
    {
        _next = next;
        _setupState = setupState;
        _setupServer = setupServer;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_setupState.IsSetupRequired)
        {
            await _next(context);
            return;
        }

        string path = context.Request.Path.Value?.TrimEnd('/') ?? "";
        string pathLower = path.ToLowerInvariant();

        // Routes handled directly by the setup server (no auth needed)
        foreach (string route in SetupHandledRoutes)
        {
            if (pathLower == route || pathLower.StartsWith(route + "/"))
            {
                await _setupServer.HandleRequest(context);
                return;
            }
        }

        // Routes that pass through to the normal pipeline
        foreach (string route in PassthroughRoutes)
        {
            if (pathLower == route || pathLower.StartsWith(route + "/"))
            {
                await _next(context);
                return;
            }
        }

        // Everything else is blocked during setup
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        object response = new
        {
            status = "setup_required",
            message = "Server is in setup mode",
            setup_url = "/setup"
        };

        string json = JsonConvert.SerializeObject(response, Formatting.Indented);
        await context.Response.WriteAsync(json, Encoding.UTF8);
    }
}
