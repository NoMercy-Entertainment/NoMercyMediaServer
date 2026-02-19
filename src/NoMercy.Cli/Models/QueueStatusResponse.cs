using Newtonsoft.Json;

namespace NoMercy.Cli.Models;

internal class QueueStatusResponse
{
    [JsonProperty("workers")] public Dictionary<string, WorkerStatusResponse> Workers { get; set; } = new();
    [JsonProperty("pending_jobs")] public int PendingJobs { get; set; }
    [JsonProperty("failed_jobs")] public int FailedJobs { get; set; }
}

internal class WorkerStatusResponse
{
    [JsonProperty("active_threads")] public int ActiveThreads { get; set; }
}
