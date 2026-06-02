using Newtonsoft.Json;

namespace NoMercy.Data.Requests;

public class FolderRequest
{
    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("driver_id")]
    public Ulid DriverId { get; set; }
}
