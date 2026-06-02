using Newtonsoft.Json;

namespace NoMercy.Data.Requests;

public class ProfilesRequest
{
    [JsonProperty("profiles")]
    public string[] Profiles { get; set; } = [];
}
