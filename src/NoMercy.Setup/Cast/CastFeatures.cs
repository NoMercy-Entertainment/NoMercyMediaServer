using Newtonsoft.Json;

namespace NoMercy.Setup.Cast;

/// <summary>
/// Optional dev-only feature flags carried in <see cref="LaunchCustomData"/>.
/// </summary>
public class CastFeatures
{
    [JsonProperty("debug", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Debug { get; set; }

    [JsonProperty("skip_auth", NullValueHandling = NullValueHandling.Ignore)]
    public bool? SkipAuth { get; set; }
}
