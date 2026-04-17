using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Composition;

namespace NoMercy.Api.Controllers.V1.Worker;

/// <summary>
/// Coordinator-side endpoint that streams task source files to remote
/// workers over HTTP. Called by <c>HttpSourceFetcher</c> when a worker
/// can't see the original path on its own filesystem (WAN deployment,
/// no shared NAS, etc.).
///
/// Security:
///   1. Signature: <c>sig</c> is HMAC-SHA256 over <c>{path}|{ts}</c>
///      using the shared <see cref="EncoderOptions.DistributedEncodingSigningKey"/>.
///      Rejects anything that doesn't verify.
///   2. Freshness: <c>ts</c> must be within 5 minutes of now. Stops
///      someone capturing a signed URL and replaying it days later.
///   3. Path allowlist: the path must correspond to a known VideoFile
///      in the library. Prevents a coordinator with a leaked signing
///      key from being used as a general file-read oracle.
///
/// Returns 503 when distributed encoding isn't enabled.
/// </summary>
[ApiController]
[Tags("Worker Source")]
[ApiVersion(1.0)]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/worker-source")]
public class WorkerSourceController(
    IDbContextFactory<MediaContext> contextFactory,
    EncoderOptions encoderOptions,
    ILogger<WorkerSourceController> logger
) : BaseController
{
    private static readonly TimeSpan MaxSignatureAge = TimeSpan.FromMinutes(5);

    [HttpGet]
    public async Task<IActionResult> Stream(
        [FromQuery] string path,
        [FromQuery] long ts,
        [FromQuery] string sig,
        CancellationToken ct
    )
    {
        if (!encoderOptions.IsDistributedEncodingEnabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(sig))
            return BadRequest(new { error = "path and sig query parameters are required" });

        // Freshness check first — cheap reject for stale / replayed requests.
        DateTimeOffset requestTime = DateTimeOffset.FromUnixTimeSeconds(ts);
        if ((DateTimeOffset.UtcNow - requestTime).Duration() > MaxSignatureAge)
        {
            logger.LogWarning("Rejected worker-source request: signature too old");
            return Unauthorized(new { error = "signature expired" });
        }

        // Signature verification.
        byte[] key = encoderOptions.GetDistributedEncodingSigningKey();
        string expectedInput = $"{path}|{ts}";
        using HMACSHA256 hmac = new(key);
        string expectedSig = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(expectedInput))
        );

        if (
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(sig),
                Encoding.UTF8.GetBytes(expectedSig)
            )
        )
        {
            logger.LogWarning(
                "Rejected worker-source request: signature mismatch for path {Path}",
                path
            );
            return Unauthorized(new { error = "signature invalid" });
        }

        // Library membership check — only serve paths the server already
        // knows about. Prevents using the signed endpoint as a generic
        // file-read oracle if someone obtains the signing key.
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        bool isKnownFile = await context.VideoFiles.AnyAsync(
            v => v.HostFolder + Path.DirectorySeparatorChar + v.Filename == path,
            ct
        );
        if (!isKnownFile)
        {
            // Try forward-slash variant too — Windows hosts may store with
            // backslashes while workers (Linux) might request with slashes.
            string altPath = path.Replace('\\', '/');
            isKnownFile = await context.VideoFiles.AnyAsync(
                v => (v.HostFolder.Replace('\\', '/') + "/" + v.Filename) == altPath,
                ct
            );
        }

        if (!isKnownFile)
        {
            logger.LogWarning(
                "Rejected worker-source request: path {Path} not in VideoFiles table",
                path
            );
            return NotFoundResponse("Source file not found in library");
        }

        if (!System.IO.File.Exists(path))
        {
            logger.LogWarning(
                "Known VideoFile {Path} is missing on disk (deleted since scan?)",
                path
            );
            return NotFoundResponse("Source file missing on disk");
        }

        // PhysicalFile streams without buffering into memory. enableRangeProcessing
        // = true lets the worker issue Range requests for resume after
        // partial downloads.
        return PhysicalFile(
            path,
            contentType: "application/octet-stream",
            fileDownloadName: Path.GetFileName(path),
            enableRangeProcessing: true
        );
    }
}
