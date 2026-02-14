using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Management;

public record ManagementStatusDto
{
    [JsonProperty("status")] public string Status { get; set; } = "ok";
    [JsonProperty("server_name")] public string ServerName { get; set; } = string.Empty;
    [JsonProperty("version")] public string Version { get; set; } = string.Empty;
    [JsonProperty("platform")] public string Platform { get; set; } = string.Empty;
    [JsonProperty("architecture")] public string Architecture { get; set; } = string.Empty;
    [JsonProperty("os")] public string Os { get; set; } = string.Empty;
    [JsonProperty("uptime_seconds")] public long UptimeSeconds { get; set; }
    [JsonProperty("start_time")] public DateTime StartTime { get; set; }
    [JsonProperty("is_dev")] public bool IsDev { get; set; }
    [JsonProperty("auto_start")] public bool AutoStart { get; set; }
    [JsonProperty("update_available")] public bool UpdateAvailable { get; set; }
    [JsonProperty("latest_version")] public string? LatestVersion { get; set; }
    [JsonProperty("setup_phase")] public string? SetupPhase { get; set; }
    [JsonProperty("app_status")] public AppProcessStatusDto? AppStatus { get; set; }
}

public record AppProcessStatusDto
{
    [JsonProperty("running")] public bool Running { get; set; }
    [JsonProperty("pid")] public int? Pid { get; set; }
}

public record ManagementConfigDto
{
    [JsonProperty("internal_port")] public int InternalPort { get; set; }
    [JsonProperty("external_port")] public int ExternalPort { get; set; }
    [JsonProperty("server_name")] public string? ServerName { get; set; }
    [JsonProperty("queue_workers")] public int QueueWorkers { get; set; }
    [JsonProperty("encoder_workers")] public int EncoderWorkers { get; set; }
    [JsonProperty("cron_workers")] public int CronWorkers { get; set; }
    [JsonProperty("data_workers")] public int DataWorkers { get; set; }
    [JsonProperty("image_workers")] public int ImageWorkers { get; set; }
    [JsonProperty("file_workers")] public int FileWorkers { get; set; }
    [JsonProperty("request_workers")] public int RequestWorkers { get; set; }
    [JsonProperty("swagger")] public bool Swagger { get; set; }
}

public record ManagementConfigUpdateDto
{
    [JsonProperty("server_name")] public string? ServerName { get; set; }
    [JsonProperty("queue_workers")] public int? QueueWorkers { get; set; }
    [JsonProperty("encoder_workers")] public int? EncoderWorkers { get; set; }
    [JsonProperty("cron_workers")] public int? CronWorkers { get; set; }
    [JsonProperty("data_workers")] public int? DataWorkers { get; set; }
    [JsonProperty("image_workers")] public int? ImageWorkers { get; set; }
    [JsonProperty("file_workers")] public int? FileWorkers { get; set; }
    [JsonProperty("request_workers")] public int? RequestWorkers { get; set; }
}

public record ManagementQueueStatusDto
{
    [JsonProperty("workers")] public Dictionary<string, ManagementWorkerStatusDto> Workers { get; set; } = new();
    [JsonProperty("pending_jobs")] public int PendingJobs { get; set; }
    [JsonProperty("failed_jobs")] public int FailedJobs { get; set; }
}

public record ManagementWorkerStatusDto
{
    [JsonProperty("active_threads")] public int ActiveThreads { get; set; }
}

public record AutoStartDto
{
    [JsonProperty("enabled")] public bool Enabled { get; set; }
}
