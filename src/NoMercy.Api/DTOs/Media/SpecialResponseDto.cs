using Newtonsoft.Json;
using Special = NoMercy.Database.Models.TvShows.Special;

namespace NoMercy.Api.DTOs.Media;

public record SpecialResponseDto
{
    [JsonProperty("nextId")]
    public object NextId { get; set; } = null!;

    [JsonProperty("data")]
    public SpecialResponseItemDto? Data { get; set; }

    public SpecialResponseDto(Special special)
    {
        //
    }

    public SpecialResponseDto()
    {
        //
    }
}
