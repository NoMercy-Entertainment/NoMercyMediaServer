namespace NoMercy.Encoder.Distribution;

using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Progress;

/// <summary>
/// Pushes per-task progress to the coordinator's
/// /api/v1/distribution/workers/{id}/tasks/{taskId}/progress endpoint. Fire-
/// and-forget — the sink never awaits an HTTP response, so ffmpeg's
/// progress thread is never blocked by a slow coordinator.
///
/// Throttles to at most one POST per <see cref="PushInterval"/> per task
/// to keep bandwidth reasonable. ffmpeg emits progress every ~500 ms;
/// the coordinator only needs one every 2 s for a smooth UI.
/// </summary>
public class HttpTaskProgressSink(
    IHttpClientFactory httpClientFactory,
    EncoderOptions options,
    ILogger<HttpTaskProgressSink> logger
) : ITaskProgressSink
{
    private static readonly TimeSpan PushInterval = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, DateTime> _lastPushUtc = new();

    public void Report(string taskId, EncodingProgress progress)
    {
        // No coordinator configured (standalone worker / coordinator) → nothing
        // to push.
        if (string.IsNullOrWhiteSpace(options.CoordinatorUrl))
            return;

        DateTime now = DateTime.UtcNow;
        if (_lastPushUtc.TryGetValue(taskId, out DateTime last) && now - last < PushInterval)
            return;

        _lastPushUtc[taskId] = now;
        _ = PushAsync(taskId, progress);
    }

    private async Task PushAsync(string taskId, EncodingProgress progress)
    {
        try
        {
            HttpClient http = httpClientFactory.CreateClient("worker-progress-push");
            http.BaseAddress = new(options.CoordinatorUrl!);
            http.Timeout = TimeSpan.FromSeconds(5);

            object payload = new
            {
                task_id = taskId,
                percent_complete = progress.PercentComplete,
                current_fps = progress.CurrentFps,
                current_speed = progress.CurrentSpeed,
                current_stage = progress.CurrentStage,
                current_operation = progress.CurrentOperation,
                elapsed_seconds = progress.Elapsed.TotalSeconds,
                estimated_remaining_seconds = progress.EstimatedRemaining?.TotalSeconds,
                bitrate_kbps = progress.BitrateKbps,
                current_time_seconds = progress.CurrentTimeSeconds,
                duration_seconds = progress.DurationSeconds,
            };

            HttpResponseMessage response = await http.PostAsJsonAsync(
                $"api/v1/distribution/workers/{options.WorkerId}/tasks/{taskId}/progress",
                payload
            );

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug(
                    "Coordinator returned {Status} for progress push (task {TaskId})",
                    (int)response.StatusCode,
                    taskId
                );
            }
        }
        catch (Exception ex)
        {
            // Progress pushes are best-effort. Swallow everything so the
            // encode doesn't fail on a transient network issue.
            logger.LogDebug(ex, "Progress push failed for task {TaskId}", taskId);
        }
    }
}
