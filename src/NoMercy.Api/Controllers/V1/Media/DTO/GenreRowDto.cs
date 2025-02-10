using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record GenreRowDto<T>
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("moreLink")] public Uri? MoreLink { get; set; }
    [JsonProperty("items")] public IEnumerable<T?> Items { get; set; } = [];

    [NotMapped]
    [System.Text.Json.Serialization.JsonIgnore]
    [JsonProperty("source")]
    public IEnumerable<HomeSourceDto> Source { get; set; } = [];
}
