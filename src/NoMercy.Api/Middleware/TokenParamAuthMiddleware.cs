using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.Middleware;

public class TokenParamAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.Headers.Authorization = context
            .Request.Headers.Authorization.ToString()
            .Split(",")
            .ElementAt(0)
            .Split("&")
            .ElementAt(0);

        // Extract JWT from query params for all requests (enables ?token= and ?access_token= everywhere)
        if (!context.Request.Headers.Authorization.ToString().Contains("Bearer"))
        {
            string jwt = context
                .Request.Query.FirstOrDefault(q => q.Key is "token" or "access_token")
                .Value.ToString();

            if (!string.IsNullOrEmpty(jwt))
            {
                context.Request.Headers.Authorization = new("Bearer " + jwt);
            }
        }

        string url = context.Request.Path;

        if (
            !ClaimsPrincipleExtensions.FolderIds.Any(x => url.StartsWith("/" + x))
            || context.Request.Headers.Authorization.ToString().Contains("Bearer")
        )
        {
            await next(context);
            return;
        }

        string? claim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(claim))
        {
            Logger.Http("Unauthorized request, no jwt: " + url);
            await WriteProblemAsync(
                context,
                statusCode: (int)HttpStatusCode.Unauthorized,
                type: "https://nomercy.tv/problems/no-token",
                title: "Authentication required",
                detail: "No bearer token was provided. Include a valid JWT in the Authorization header or as an access_token query parameter.",
                authError: "NO_TOKEN"
            );
            return;
        }

        if (!Guid.TryParse(claim, out Guid userId) || userId == Guid.Empty)
        {
            Logger.Http("Unauthorized request, guid malformed or empty: " + url);
            await WriteProblemAsync(
                context,
                statusCode: (int)HttpStatusCode.Forbidden,
                type: "https://nomercy.tv/problems/invalid-token",
                title: "Invalid token",
                detail: "The token subject (sub) is not a valid GUID. The token may be malformed.",
                authError: "INVALID_TOKEN"
            );
            return;
        }

        User? user = ClaimsPrincipleExtensions.Users.FirstOrDefault(x => x.Id.Equals(userId));

        if (user is null)
        {
            Logger.Http("Unauthorized request, user not found: " + url);
            await WriteProblemAsync(
                context,
                statusCode: (int)HttpStatusCode.Forbidden,
                type: "https://nomercy.tv/problems/user-not-found",
                title: "User not found",
                detail: "The authenticated user is not registered on this server. Ask the server owner to add your account.",
                authError: "USER_NOT_FOUND"
            );
            return;
        }

        await next(context);
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string type,
        string title,
        string detail,
        string authError
    )
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        object body = new
        {
            type,
            title,
            status = statusCode,
            detail,
            instance = context.Request.Path.Value,
            authError,
        };

        await context.Response.WriteAsync(JsonConvert.SerializeObject(body), Encoding.UTF8);
    }
}
