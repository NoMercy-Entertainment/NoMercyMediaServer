using Newtonsoft.Json;

namespace NoMercy.Api.DTOs.Media;

public record HomeResponseDto<T>
{
    [JsonProperty("data")]
    public IEnumerable<GenreRowDto<T>> Data { get; set; } = [];
}
