using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record PermissionsDto
{
    [JsonProperty("edit")] public bool Edit { get; set; }
}