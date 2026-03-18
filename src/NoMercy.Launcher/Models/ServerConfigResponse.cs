using Newtonsoft.Json;

namespace NoMercy.Launcher.Models;

public class ServerConfigResponse
{
    [JsonProperty("internal_port")] public int InternalPort { get; set; }
    [JsonProperty("external_port")] public int ExternalPort { get; set; }
    [JsonProperty("server_name")] public string? ServerName { get; set; }
    [JsonProperty("library_workers")] public int LibraryWorkers { get; set; }
    [JsonProperty("import_workers")] public int ImportWorkers { get; set; }
    [JsonProperty("extras_workers")] public int ExtrasWorkers { get; set; }
    [JsonProperty("encoder_workers")] public int EncoderWorkers { get; set; }
    [JsonProperty("cron_workers")] public int CronWorkers { get; set; }
    [JsonProperty("image_workers")] public int ImageWorkers { get; set; }
    [JsonProperty("file_workers")] public int FileWorkers { get; set; }
    [JsonProperty("music_workers")] public int MusicWorkers { get; set; }
    [JsonProperty("swagger")] public bool Swagger { get; set; }
}
