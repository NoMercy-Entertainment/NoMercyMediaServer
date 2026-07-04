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
using Microsoft.Extensions.Logging;

namespace NoMercy.Encoder.Distribution;

/// <inheritdoc />
public class WorkerInputResolver(
    ITaskSerializer serializer,
    ISourceFetcher sourceFetcher,
    ILogger<WorkerInputResolver> logger
) : IWorkerInputResolver
{
    public async Task<WorkerInputResolution> ResolveAsync(
        string payload,
        byte[] signingKey,
        CancellationToken ct
    )
    {
        EncodeTask? task = serializer.Deserialize(payload, signingKey);
        if (task is null)
        {
            logger.LogWarning(
                "Worker rejected task payload — signature invalid or payload expired"
            );
            return new WorkerInputResolution();
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
            return new WorkerInputResolution
            {
                Task = task,
                SourceFetchFailed = true,
                SourceFetchError = ex.Message,
            };
        }

        return new WorkerInputResolution { Task = task, EffectiveTask = effectiveTask };
    }

    public Task ReleaseAsync(EncodeTask task) => sourceFetcher.ReleaseAsync(task);

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
