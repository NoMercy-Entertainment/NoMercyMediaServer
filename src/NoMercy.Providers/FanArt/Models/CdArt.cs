using Newtonsoft.Json;

namespace NoMercy.Providers.FanArt.Models;

public class CdArt : Image
{
    [JsonProperty("disc")] public int Disc { get; set; }
    [JsonProperty("size")] public int Size { get; set; }
}