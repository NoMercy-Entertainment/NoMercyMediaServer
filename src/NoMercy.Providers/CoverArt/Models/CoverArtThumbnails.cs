using Newtonsoft.Json;
using NoMercy.Providers.Helpers;

namespace NoMercy.Providers.CoverArt.Models;
public class CoverArtThumbnails
{
    private Uri __250 { get; set;} = null!;
    private Uri __500 { get; set;} = null!;
    private Uri __1200 { get; set;} = null!;
    // ReSharper disable once InconsistentNaming
    private Uri __large { get; set;} = null!;
    // ReSharper disable once InconsistentNaming
    private Uri __small { get; set;} = null!;

    [JsonProperty("250")]
    public Uri _250
    {
        get => __250.ToHttps();
        init => __250 = value;
    }

    [JsonProperty("500")]
    public Uri _500
    {
        get => __500.ToHttps();
        init => __500 = value;
    }

    [JsonProperty("1200")]
    public Uri _1200
    {
        get => __1200.ToHttps();
        init => __1200 = value;
    }

    [JsonProperty("large")]
    public Uri Large
    {
        get => __large.ToHttps();
        init => __large = value;
    }

    [JsonProperty("small")]
    public Uri Small
    {
        get => __small.ToHttps();
        init => __small = value;
    }
}