using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoMercy.Api.DTOs.Media;

public class NmCarouselDto<T>
{
    [JsonProperty("id")] public dynamic Id { get; set; } = string.Empty;
    [JsonProperty("next_id")] public dynamic NextId { get; set; } = Ulid.NewUlid();
    [JsonProperty("previous_id")] public dynamic PreviousId { get; set; } = Ulid.NewUlid();
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
    [JsonProperty("more_link")] public Uri? MoreLink { get; set; }
    [JsonProperty("items")] public List<T> Items { get; set; } = [];

    [NotMapped]
    [JsonIgnore]
    [JsonProperty("source")]
    public IEnumerable<HomeSourceDto> Source { get; set; } = [];
}