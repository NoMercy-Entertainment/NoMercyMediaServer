using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media;

public class FavoriteRequest
{
    [JsonProperty("id")] public string Id { get; set; } = "";
    [JsonProperty("type")] public string Type { get; set; } = "";
}