using Newtonsoft.Json;
using NoMercy.Helpers;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record WallpaperRequest
{
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
    [JsonProperty("color")] public string? Color { get; set; } = string.Empty;
    [JsonProperty("style")] public WallpaperStyle Style { get; set; } = WallpaperStyle.Fill;
}