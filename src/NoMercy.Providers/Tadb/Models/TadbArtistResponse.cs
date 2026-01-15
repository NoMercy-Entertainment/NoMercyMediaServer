using Newtonsoft.Json;

namespace NoMercy.Providers.Tadb.Models;

public class TadbArtistResponse
{
    [JsonProperty("artists")] public TadbArtist[]? Artists { get; set; }
}