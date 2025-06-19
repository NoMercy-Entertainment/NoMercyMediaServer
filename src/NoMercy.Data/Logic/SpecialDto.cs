using Newtonsoft.Json;

namespace NoMercy.Data.Logic;

public class SpecialDto
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("backdrop")] public string Backdrop { get; set; } = string.Empty;
    [JsonProperty("poster")] public string Poster { get; set; } = string.Empty;
    [JsonProperty("logo")] public string Logo { get; set; } = string.Empty;
    [JsonProperty("description")] public string Description { get; set; } = string.Empty;
    [JsonProperty("Item")] public SpecialItemDto[] Item { get; set; } = [];
    [JsonProperty("creator")] public string Creator { get; set; } = string.Empty;
}