using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;

namespace NoMercy.Api.Middleware;

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
///
/// Remote-worker token introspection (Phase 4.9)
/// ──────────────────────────────────────────────
/// When a request carries X-NoMercy-WorkerToken the middleware first
/// checks whether that token is active via
/// <see cref="ILicenseTokenClient.IntrospectAsync"/>.  If active the
/// token's secret is used to verify the HMAC; if not the request is
/// rejected with 401.  The introspect result is cached for 30 s inside
/// the client — this path does not block on network I/O for every request.
/// </summary>
public class HmacValidationMiddleware(
    RequestDelegate next,
    IOptions<EncoderOptions> encoderOptions,
    ILicenseTokenClient? licenseTokenClient = null
)
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

        // Determine the signing secret.
        // Remote-worker path: X-NoMercy-WorkerToken is present → introspect it.
        string? workerToken = context.Request.Headers["X-NoMercy-WorkerToken"].ToString();
        string? secret;

        if (!string.IsNullOrWhiteSpace(workerToken) && licenseTokenClient is not null)
        {
            IntrospectResult introspect = await licenseTokenClient
                .IntrospectAsync(workerToken, context.RequestAborted)
                .ConfigureAwait(false);

            if (!introspect.Active)
            {
                await WriteHmacError(
                    context,
                    "worker_token_invalid",
                    introspect.Message ?? "Worker token is not active"
                );
                return;
            }

            // The worker token IS the secret used for HMAC on this request.
            secret = workerToken;
        }
        else
        {
            secret = encoderOptions.Value.DistributedEncodingSigningKey;
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            // Signing key absent but path is protected — reject rather than
            // pass through. A missing key means distributed encoding is
            // misconfigured; passing through silently would bypass all HMAC
            // authentication on these endpoints.
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"error\":\"hmac_unavailable\",\"detail\":\"Distributed encoding signing key is not configured.\"}",
                Encoding.UTF8
            );
            return;
        }

        if (
            !context.Request.Headers.TryGetValue("X-NoMercy-Timestamp", out StringValues tsHeader)
            || !long.TryParse(tsHeader.ToString(), out long timestamp)
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
            !context.Request.Headers.TryGetValue("X-NoMercy-Signature", out StringValues sigHeader)
            || string.IsNullOrWhiteSpace(sigHeader.ToString())
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
