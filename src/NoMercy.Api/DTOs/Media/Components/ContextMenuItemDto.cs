using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media.Components;

public record ContextMenuItemDto
{
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("action")] public string? Action { get; set; }
    [JsonProperty("icon")] public string? Icon { get; set; }
    [JsonProperty("destructive")] public bool Destructive { get; set; }
    [JsonProperty("method")] public string? Method { get; set; }
    [JsonProperty("confirm")] public string? Confirm { get; set; }
    [JsonProperty("args")] public Dictionary<string, object>? Args { get; set; }
}
