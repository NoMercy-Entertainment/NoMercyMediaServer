using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO.Components;

/// <summary>
/// Context menu item for component actions.
/// </summary>
public record ContextMenuItem
{
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("action")] public string? Action { get; set; }
    [JsonProperty("icon")] public string? Icon { get; set; }
    [JsonProperty("destructive")] public bool Destructive { get; set; }
}
