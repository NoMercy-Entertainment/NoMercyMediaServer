// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Encoder.Composition;
using NoMercy.Storage;

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
// Primary route per the encoder spec.
[Route("api/v{version:apiVersion}/worker/source")]
// Legacy alias — kept for backwards compatibility with workers on older builds.
[Obsolete("Use /api/v{version}/worker/source — kept for backwards compatibility")]
[Route("api/v{version:apiVersion}/worker-source")]
public class WorkerSourceController(
    IDbContextFactory<MediaContext> contextFactory,
    EncoderOptions encoderOptions,
    ILogger<WorkerSourceController> logger,
    IStorage storage
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
        // VideoFile.HostFolder/Filename are normalised to forward-slash by
        // model setters, so a single forward-slash comparison covers both
        // Windows hosts and Linux workers.
        string normalizedPath = path.Replace('\\', '/');
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        bool isKnownFile = await context.VideoFiles.AnyAsync(
            v => v.HostFolder + "/" + v.Filename == normalizedPath,
            ct
        );

        if (!isKnownFile)
        {
            logger.LogWarning(
                "Rejected worker-source request: path {Path} not in VideoFiles table",
                path
            );
            return NotFoundResponse("Source file not found in library");
        }

        if (!storage.Exists(path))
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
