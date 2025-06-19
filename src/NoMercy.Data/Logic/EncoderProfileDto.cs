using Newtonsoft.Json;

namespace NoMercy.Data.Logic;

public class EncoderProfileDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("container")] public string Container { get; set; } = string.Empty;
    [JsonProperty("params")] public EncoderProfileParamsDto Params { get; set; } = new();
}