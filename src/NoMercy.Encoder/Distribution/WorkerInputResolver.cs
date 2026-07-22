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
        EncodeTask? task = serializer.Deserialize(payload: payload, signingKey: signingKey);
        if (task is null)
        {
            logger.LogWarning(
                message: "Worker rejected task payload — signature invalid or payload expired"
            );
            return new();
        }

        logger.LogInformation(message: "Worker executing task {TaskId} ({Type})", args: [task.TaskId, task.Type]);

        // Pull the source locally if the worker can't see the original
        // path on its own filesystem. Shared-storage installs return the
        // path unchanged; WAN workers stream from the coordinator.
        EncodeTask effectiveTask = task;
        try
        {
            string localSourcePath = await sourceFetcher.EnsureLocalAsync(task: task, ct: ct);
            if (
                !string.IsNullOrEmpty(value: localSourcePath)
                && !string.Equals(a: localSourcePath, b: task.InputPath, comparisonType: StringComparison.Ordinal)
            )
            {
                effectiveTask = RewriteInputPath(task: task, localPath: localSourcePath);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(exception: ex, message: "Source fetch failed for task {TaskId}", args: task.TaskId);
            return new()
            {
                Task = task,
                SourceFetchFailed = true,
                SourceFetchError = ex.Message,
            };
        }

        return new() { Task = task, EffectiveTask = effectiveTask };
    }

    public Task ReleaseAsync(EncodeTask task) => sourceFetcher.ReleaseAsync(task: task);

    /// <summary>
    /// Rewrites the task's command arguments to swap the original input
    /// path for the local cached path. Finds the original InputPath
    /// verbatim in the argument list and replaces it — safe because
    /// EncodeTask construction upstream embeds the same string in both
    /// Command.Arguments and task.InputPath.
    /// </summary>
    private static EncodeTask RewriteInputPath(EncodeTask task, string localPath)
    {
        if (string.IsNullOrEmpty(value: task.InputPath))
            return task;

        string[] newArgs = task
            .Command.Arguments.Select(selector: arg =>
                string.Equals(a: arg, b: task.InputPath, comparisonType: StringComparison.Ordinal) ? localPath : arg
            )
            .ToArray();

        return task with
        {
            Command = task.Command with { Arguments = newArgs },
            InputPath = localPath,
        };
    }
}
