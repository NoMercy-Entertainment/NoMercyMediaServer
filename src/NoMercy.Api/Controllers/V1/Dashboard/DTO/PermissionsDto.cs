using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

public record PermissionsDto
{
    [JsonProperty("edit")] public bool Edit { get; set; }
}