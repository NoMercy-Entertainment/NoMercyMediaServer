using Newtonsoft.Json;

namespace NoMercy.Providers.FanArt.Models;

public class Albums
{
    private Image[]? _cover = [];
    private Image[]? _cdart = [];

    [JsonProperty("albumcover")]
    public Image[] Cover
    {
        get => _cover ?? [];
        set => _cover = value;
    }

    [JsonProperty("cdart")]
    public Image[] CdArt
    {
        get => _cdart ?? [];
        set => _cdart = value;
    }
}