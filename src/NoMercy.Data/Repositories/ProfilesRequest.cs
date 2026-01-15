using Newtonsoft.Json;

namespace NoMercy.Data.Repositories;

public class ProfilesRequest
{
    [JsonProperty("profiles")] public string[] Profiles { get; set; } = [];
}