using Newtonsoft.Json;

namespace NoMercy.Tray.Models;

public class ServerConfigResponse
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
