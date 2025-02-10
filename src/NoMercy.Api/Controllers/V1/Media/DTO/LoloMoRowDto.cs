using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record LoloMoRowDto<T>
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("moreLink")] public string MoreLink { get; set; } = string.Empty;
    [JsonProperty("items")] public IEnumerable<T> Items { get; set; } = [];
}