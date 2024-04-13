using System.Security.Claims;
using System.Web;
using NoMercy.Database.Models;
using NoMercy.Helpers;

namespace NoMercy.Server.app.Http.Middleware;

public class AccessLogMiddleware
{
    
    private readonly RequestDelegate _next;
    
    private readonly string[] _ignoredStartsWithRoutes =
        [
            "/images",
        ];

    private readonly string[] _ignoreExact =
        [
            "/",
            "/swagger",
            "/swagger-ui",
            "/swagger-ui/favicon-16x16.png",
            "/swagger-ui/favicon-32x32.png",
            "/swagger-ui/favicon.ico",
            "/swagger-ui/index.html",
            "/swagger-ui/swagger-ui-bundle.js",
            "/swagger-ui/swagger-ui-standalone-preset.js",
            "/swagger-ui/swagger-ui.css",
            "/swagger/v1/swagger.json",
        ];

    private readonly string[] _ignoreIfAuthenticated = [
        "/socket",
    ];
    
    private readonly string[] _ignoreIfGuest = [
        "/status",
    ];

    public AccessLogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        
        string path = HttpUtility.UrlDecode(context.Request.Path);
        
        bool ignoreStart = _ignoredStartsWithRoutes
            .Any(route => context.Request.Path.ToString().StartsWith(route));
        
        bool ignoreExactRoute = _ignoreExact
            .Any(route => context.Request.Path.ToString().Equals(route));
        
        if (ignoreStart || ignoreExactRoute)
        {
            await _next(context);
            return;
        }
        
        bool ignoreIfGuest = _ignoreIfGuest
            .Any(route => context.Request.Path.ToString().Equals(route));
        
        string? guid = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (guid is null)
        {
            if(ignoreIfGuest)
            {
                await _next(context);
                return;
            }
            
            Logger.Http($"Unknown: {context.Connection.RemoteIpAddress}: {path}");
            await _next(context);
            return;
        }
        

        Guid userId = Guid.Parse(guid);
        if(userId == Guid.Empty)
        {
            if(ignoreIfGuest) 
            {
                await _next(context);
                return;
            }
            
            Logger.Http($"Unknown: {context.Connection.RemoteIpAddress}: {path}");
            await _next(context);
            return;
        }
        
        bool ignoreIfAuthenticated = _ignoreIfAuthenticated
            .Any(route => context.Request.Path.ToString().Equals(route));
            
        if(ignoreIfAuthenticated) 
        {
            await _next(context);
            return;
        }
        
        // if (!TokenParamAuthMiddleware.FolderIds.Any(x => path.StartsWith("/" + x)))
        // {
        //     await _next(context);
        //     return;
        // }
        
        User? user = TokenParamAuthMiddleware.Users.FirstOrDefault(x => x.Id == userId);
        
        Logger.Http($"{user?.Name ?? $"Unknown: {context.Connection.RemoteIpAddress}:"}: {path}");
        
        await _next(context);
    }
}