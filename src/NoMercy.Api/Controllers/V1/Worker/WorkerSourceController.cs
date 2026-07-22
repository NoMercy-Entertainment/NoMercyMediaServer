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
[Tags(tags: "Worker Source")]
[ApiVersion(version: 1.0)]
[AllowAnonymous]
// Primary route per the encoder spec.
[Route(template: "api/v{version:apiVersion}/worker/source")]
// Legacy alias — kept for backwards compatibility with workers on older builds.
[Obsolete(message: "Use /api/v{version}/worker/source — kept for backwards compatibility")]
[Route(template: "api/v{version:apiVersion}/worker-source")]
public class WorkerSourceController(
    IDbContextFactory<MediaContext> contextFactory,
    EncoderOptions encoderOptions,
    ILogger<WorkerSourceController> logger,
    IStorage storage
) : BaseController
{
    private static readonly TimeSpan MaxSignatureAge = TimeSpan.FromMinutes(minutes: 5);

    [HttpGet]
    public async Task<IActionResult> Stream(
        [FromQuery] string path,
        [FromQuery] long ts,
        [FromQuery] string sig,
        CancellationToken ct
    )
    {
        if (!encoderOptions.IsDistributedEncodingEnabled)
            return ServiceUnavailableResponse(
                detail: "Distributed encoding is not enabled on this server."
            );

        if (string.IsNullOrWhiteSpace(value: path) || string.IsNullOrWhiteSpace(value: sig))
            return BadRequestResponse(detail: "path and sig query parameters are required");

        // Freshness check first — cheap reject for stale / replayed requests.
        DateTimeOffset requestTime = DateTimeOffset.FromUnixTimeSeconds(seconds: ts);
        if ((DateTimeOffset.UtcNow - requestTime).Duration() > MaxSignatureAge)
        {
            logger.LogWarning(message: "Rejected worker-source request: signature too old");
            return UnauthenticatedResponse(detail: "signature expired");
        }

        // Signature verification.
        byte[] key = encoderOptions.GetDistributedEncodingSigningKey();
        string expectedInput = $"{path}|{ts}";
        using HMACSHA256 hmac = new(key: key);
        string expectedSig = Convert.ToBase64String(
            inArray: hmac.ComputeHash(buffer: Encoding.UTF8.GetBytes(s: expectedInput))
        );

        if (
            !CryptographicOperations.FixedTimeEquals(
                left: Encoding.UTF8.GetBytes(s: sig),
                right: Encoding.UTF8.GetBytes(s: expectedSig)
            )
        )
        {
            logger.LogWarning(
                message: "Rejected worker-source request: signature mismatch for path {Path}",
                args: path
            );
            return UnauthenticatedResponse(detail: "signature invalid");
        }

        // Library membership check — only serve paths the server already
        // knows about. Prevents using the signed endpoint as a generic
        // file-read oracle if someone obtains the signing key.
        // VideoFile.HostFolder/Filename are normalised to forward-slash by
        // model setters, so a single forward-slash comparison covers both
        // Windows hosts and Linux workers.
        string normalizedPath = path.Replace(oldChar: '\\', newChar: '/');
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        bool isKnownFile = await context.VideoFiles.AnyAsync(
            predicate: v => v.HostFolder + "/" + v.Filename == normalizedPath,
            cancellationToken: ct
        );

        if (!isKnownFile)
        {
            logger.LogWarning(
                message: "Rejected worker-source request: path {Path} not in VideoFiles table",
                args: path
            );
            return NotFoundResponse(detail: "Source file not found in library");
        }

        if (!storage.Exists(path: path))
        {
            logger.LogWarning(
                message: "Known VideoFile {Path} is missing on disk (deleted since scan?)",
                args: path
            );
            return NotFoundResponse(detail: "Source file missing on disk");
        }

        // PhysicalFile streams without buffering into memory. enableRangeProcessing
        // = true lets the worker issue Range requests for resume after
        // partial downloads.
        return PhysicalFile(
            physicalPath: path,
            contentType: "application/octet-stream",
            fileDownloadName: Path.GetFileName(path: path),
            enableRangeProcessing: true
        );
    }
}
