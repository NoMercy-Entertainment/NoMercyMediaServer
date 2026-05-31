using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
[Tags("Worker Execution")]
[ApiVersion(1.0)]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/worker")]
public class WorkerExecutionController(
    LocalWorkerDispatcher localDispatcher,
    ITaskSerializer serializer,
    ISourceFetcher sourceFetcher,
    EncoderOptions encoderOptions,
    ILogger<WorkerExecutionController> logger
) : BaseController
{
    [HttpPost("tasks")]
    [HttpPost("execute-task")]
    public async Task<IActionResult> ExecuteTask(CancellationToken ct)
    {
        if (!encoderOptions.IsDistributedEncodingEnabled)
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "Distributed encoding is not enabled on this worker. "
                        + "Set DistributedEncodingSigningKey in EncoderOptions and restart.",
                }
            );

        // Read the raw body — the payload is a signed JSON envelope.
        string payload;
        using (StreamReader reader = new(Request.Body))
            payload = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(payload))
            return BadRequest(new { error = "Empty request body" });

        byte[] signingKey = encoderOptions.GetDistributedEncodingSigningKey();
        EncodeTask? task = serializer.Deserialize(payload, signingKey);

        if (task is null)
        {
            logger.LogWarning(
                "Worker rejected task payload — signature invalid, expired, or malformed"
            );
            return Unauthorized(new { error = "Task payload failed HMAC verification or expired" });
        }

        logger.LogInformation("Worker executing task {TaskId} ({Type})", task.TaskId, task.Type);

        // Pull the source locally if the worker can't see the original
        // path on its own filesystem. Shared-storage installs return the
        // path unchanged; WAN workers stream from the coordinator.
        EncodeTask effectiveTask = task;
        try
        {
            string localSourcePath = await sourceFetcher.EnsureLocalAsync(task, ct);
            if (
                !string.IsNullOrEmpty(localSourcePath)
                && !string.Equals(localSourcePath, task.InputPath, StringComparison.Ordinal)
            )
            {
                effectiveTask = RewriteInputPath(task, localSourcePath);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Source fetch failed for task {TaskId}", task.TaskId);
            DispatchResult failedFetch = new(
                TaskId: task.TaskId,
                Success: false,
                OutputPath: task.OutputPath,
                Duration: TimeSpan.Zero,
                Error: $"Source fetch failed: {ex.Message}"
            );
            return Content(serializer.SerializeResult(failedFetch, signingKey), "application/json");
        }

        try
        {
            DispatchResult[] results = await localDispatcher.DispatchAsync([effectiveTask], ct);
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

            string signedResponse = serializer.SerializeResult(result, signingKey);
            return Content(signedResponse, "application/json");
        }
        finally
        {
            // Always release the cached source — the encode is either done
            // or failed. Next retry will re-fetch if needed.
            await sourceFetcher.ReleaseAsync(task);
        }
    }

    /// <summary>
    /// Rewrites the task's command arguments to swap the original input
    /// path for the local cached path. Finds the original InputPath
    /// verbatim in the argument list and replaces it — safe because
    /// EncodeTask construction upstream embeds the same string in both
    /// Command.Arguments and task.InputPath.
    /// </summary>
    private static EncodeTask RewriteInputPath(EncodeTask task, string localPath)
    {
        if (string.IsNullOrEmpty(task.InputPath))
            return task;

        string[] newArgs = task
            .Command.Arguments.Select(arg =>
                string.Equals(arg, task.InputPath, StringComparison.Ordinal) ? localPath : arg
            )
            .ToArray();

        return task with
        {
            Command = task.Command with { Arguments = newArgs },
            InputPath = localPath,
        };
    }
}
