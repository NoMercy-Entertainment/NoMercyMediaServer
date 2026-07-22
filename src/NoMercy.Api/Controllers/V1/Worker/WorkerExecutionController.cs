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
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;

namespace NoMercy.Api.Controllers.V1.Worker;

/// <summary>
/// Server-to-server receiver for distributed encode tasks. A coordinator
/// POSTs a signed <see cref="EncodeTask"/> here; the worker runs it
/// through the local dispatcher and returns a signed
/// <see cref="DispatchResult"/>.
///
/// Auth is the HMAC signature on the payload — no bearer token required.
/// Anyone without the shared signing key gets rejected at the serializer
/// layer before any ffmpeg process spawns. <see cref="AllowAnonymous"/>
/// is deliberate: adding Keycloak auth on top of HMAC would block
/// legitimate worker-to-coordinator calls without the user having a
/// valid session (workers are headless processes).
///
/// 503 when distributed encoding isn't enabled on this install — workers
/// are meant to be explicit opt-in, not ambient.
/// </summary>
[ApiController]
[Tags(tags: "Worker Execution")]
[ApiVersion(version: 1.0)]
[AllowAnonymous]
[Route(template: "api/v{version:apiVersion}/worker")]
public class WorkerExecutionController(
    LocalWorkerDispatcher localDispatcher,
    ITaskSerializer serializer,
    IWorkerInputResolver inputResolver,
    EncoderOptions encoderOptions
) : BaseController
{
    [HttpPost(template: "tasks")]
    [HttpPost(template: "execute-task")]
    public async Task<IActionResult> ExecuteTask(CancellationToken ct)
    {
        if (!encoderOptions.IsDistributedEncodingEnabled)
            return ServiceUnavailableResponse(
                detail: "Distributed encoding is not enabled on this worker. "
                        + "Set DistributedEncodingSigningKey in EncoderOptions and restart."
            );

        // Read the raw body — the payload is a signed JSON envelope.
        string payload;
        using (StreamReader reader = new(stream: Request.Body))
            payload = await reader.ReadToEndAsync(cancellationToken: ct);

        if (string.IsNullOrWhiteSpace(value: payload))
            return BadRequestResponse(detail: "Empty request body");

        byte[] signingKey = encoderOptions.GetDistributedEncodingSigningKey();
        WorkerInputResolution resolution = await inputResolver.ResolveAsync(
            payload: payload,
            signingKey: signingKey,
            ct: ct
        );

        if (resolution.Task is null)
            return UnauthenticatedResponse(detail: "Task payload failed HMAC verification or expired");

        EncodeTask task = resolution.Task;

        if (resolution.SourceFetchFailed)
        {
            DispatchResult failedFetch = new(
                TaskId: task.TaskId,
                Success: false,
                OutputPath: task.OutputPath,
                Duration: TimeSpan.Zero,
                Error: $"Source fetch failed: {resolution.SourceFetchError}"
            );
            return Content(content: serializer.SerializeResult(result: failedFetch, signingKey: signingKey), contentType: "application/json");
        }

        EncodeTask effectiveTask = resolution.EffectiveTask ?? task;
        try
        {
            DispatchResult[] results = await localDispatcher.DispatchAsync(tasks: [effectiveTask], ct: ct);
            DispatchResult result =
                results.Length > 0
                    ? results[0]
                    : new(
                        TaskId: task.TaskId,
                        Success: false,
                        OutputPath: task.OutputPath,
                        Duration: TimeSpan.Zero,
                        Error: "Local dispatcher returned no result"
                    );
            string signedResponse = serializer.SerializeResult(result: result, signingKey: signingKey);
            return Content(content: signedResponse, contentType: "application/json");
        }
        finally
        {
            // Always release the cached source — the encode is either done
            // or failed. Next retry will re-fetch if needed.
            await inputResolver.ReleaseAsync(task: task);
        }
    }
}
