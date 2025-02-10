using Newtonsoft.Json;

namespace NoMercy.Providers.Tadb.Models;
public class TadbAlbumResponse
{
    [JsonProperty("album")] public TadbAlbum[] Albums { get; set; } = [];
}