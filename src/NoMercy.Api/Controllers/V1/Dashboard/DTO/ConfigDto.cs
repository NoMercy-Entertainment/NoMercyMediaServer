using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public class ConfigDto
{
    [JsonProperty("data")] public ConfigDtoData Data { get; set; }
}

public class ConfigDtoData
{
    [JsonProperty("internal_server_port")] public int InternalServerPort { get; set; }
    [JsonProperty("external_server_port")] public int ExternalServerPort { get; set; }
    
    [JsonProperty("server_name")] public string? ServerName { get; set; }
    [JsonProperty("queue_workers")] public int? QueueWorkers { get; set; }
    [JsonProperty("encoder_workers")] public int? EncoderWorkers { get; set; }
    [JsonProperty("cron_workers")] public int? CronWorkers { get; set; }
    [JsonProperty("data_workers")] public int? DataWorkers { get; set; }
    [JsonProperty("image_workers")] public int? ImageWorkers { get; set; }
    [JsonProperty("request_workers")] public int? RequestWorkers { get; set; }
    [JsonProperty("swagger")] public bool? Swagger { get; set; }
}