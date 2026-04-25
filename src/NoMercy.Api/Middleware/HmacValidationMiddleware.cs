namespace NoMercy.Api.Middleware;

using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;

/// <summary>
/// Validates HMAC signatures on inbound requests routed to
/// /api/v1/distribution/* and /api/v1/worker/*.
///
/// Each request must carry:
///   X-NoMercy-Timestamp  — unix seconds (UTC)
///   X-NoMercy-Signature  — base64(hmac_sha256(key, stringToSign))
///
/// Replay window: 5 minutes.  Requests older than that are rejected.
///
/// Progress-push path is exempt (spec: anonymous):
///   /api/v1/distribution/workers/{id}/tasks/{id}/progress
/// </summary>
public class HmacValidationMiddleware(RequestDelegate next, IOptions<EncoderOptions> encoderOptions)
{
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);

    // Prefix segments that require HMAC.
    private static readonly string[] ProtectedPrefixes =
    [
        "/api/v1/distribution/",
        "/api/v1/worker/",
    ];

    // Paths exempt from HMAC (anonymous per spec).
    private static readonly string[] ExemptSuffixes = ["/progress"];

    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? string.Empty;

        if (!IsProtected(path))
        {
            await next(context);
            return;
        }

        if (IsExempt(path))
        {
            await next(context);
            return;
        }

        string? secret = encoderOptions.Value.DistributedEncodingSigningKey;
        if (string.IsNullOrWhiteSpace(secret))
        {
            // Distributed encoding not configured — pass through; other auth
            // layers still apply.
            await next(context);
            return;
        }

        if (
            !context.Request.Headers.TryGetValue(
                "X-NoMercy-Timestamp",
                out Microsoft.Extensions.Primitives.StringValues tsHeader
            ) || !long.TryParse(tsHeader.ToString(), out long timestamp)
        )
        {
            await WriteHmacError(
                context,
                "missing_timestamp",
                "X-NoMercy-Timestamp header is required"
            );
            return;
        }

        if (
            !context.Request.Headers.TryGetValue(
                "X-NoMercy-Signature",
                out Microsoft.Extensions.Primitives.StringValues sigHeader
            ) || string.IsNullOrWhiteSpace(sigHeader.ToString())
        )
        {
            await WriteHmacError(
                context,
                "missing_signature",
                "X-NoMercy-Signature header is required"
            );
            return;
        }

        // Buffer the body so we can both verify and still let the controller read it.
        context.Request.EnableBuffering();
        byte[] bodyBytes;
        using (MemoryStream ms = new())
        {
            await context.Request.Body.CopyToAsync(ms);
            bodyBytes = ms.ToArray();
        }

        context.Request.Body.Position = 0;

        HmacSigner signer = new(secret);
        bool valid = signer.Verify(
            context.Request.Method,
            path,
            timestamp,
            bodyBytes,
            sigHeader.ToString(),
            ReplayWindow
        );

        if (!valid)
        {
            await WriteHmacError(
                context,
                "signature_invalid",
                "HMAC signature verification failed"
            );
            return;
        }

        await next(context);
    }

    private static bool IsProtected(string path)
    {
        foreach (string prefix in ProtectedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsExempt(string path)
    {
        foreach (string suffix in ExemptSuffixes)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task WriteHmacError(HttpContext context, string reason, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        object body = new
        {
            error = "hmac_invalid",
            reason,
            detail,
        };

        await context.Response.WriteAsync(JsonConvert.SerializeObject(body), Encoding.UTF8);
    }
}
