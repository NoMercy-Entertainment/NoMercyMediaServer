using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record CountryDto
{
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("code")] public string? Code { get; set; }
}