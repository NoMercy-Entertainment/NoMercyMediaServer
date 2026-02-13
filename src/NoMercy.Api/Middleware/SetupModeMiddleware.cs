using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoMercy.Setup;

namespace NoMercy.Api.Middleware;

public class SetupModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SetupState _setupState;

    private static readonly string[] SetupRoutes =
    [
        "/setup",
        "/sso-callback",
        "/health",
        "/manage"
    ];

    public SetupModeMiddleware(RequestDelegate next, SetupState setupState)
    {
        _next = next;
        _setupState = setupState;
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

        bool isSetupRoute = false;
        foreach (string route in SetupRoutes)
        {
            if (pathLower == route || pathLower.StartsWith(route + "/"))
            {
                isSetupRoute = true;
                break;
            }
        }

        if (isSetupRoute)
        {
            await _next(context);
            return;
        }

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
