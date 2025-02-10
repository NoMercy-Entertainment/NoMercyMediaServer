using Newtonsoft.Json;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record HomeSourceDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("media_type")] public string MediaType { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    public HomeSourceDto(int id, string type)
    {
        Id = id;
        MediaType = type;
        Link = new($"/{type}/{id}", UriKind.Relative);
    }

}
