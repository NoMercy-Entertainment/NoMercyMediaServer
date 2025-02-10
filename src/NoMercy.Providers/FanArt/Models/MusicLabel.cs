using Newtonsoft.Json;
using NoMercy.Providers.Helpers;

namespace NoMercy.Providers.FanArt.Models;
public class MusicLabel
{
    // ReSharper disable once InconsistentNaming
    private Uri __url = null!;

    [JsonProperty("id")] public string Id { get; set; } = string.Empty;

    [JsonProperty("url")]
    public Uri Url
    {
        get => __url.ToHttps();
        init => __url = value;
    }

    [JsonProperty("colour")] public string Color { get; set; } = string.Empty;
    [JsonProperty("likes")] public string Likes { get; set; } = string.Empty;
}