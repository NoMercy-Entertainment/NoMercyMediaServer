using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Dashboard;

public record LanguageDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("iso_639_1")] public string? Iso6391 { get; set; }
    [JsonProperty("english_name")] public string? EnglishName { get; set; }
    [JsonProperty("name")] public string? Name { get; set; }
}