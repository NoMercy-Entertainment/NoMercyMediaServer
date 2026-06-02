using Newtonsoft.Json;

namespace NoMercy.Data.Requests;

public class RescanLibraryRequest
{
    [JsonProperty("id")]
    public bool ForceUpdate { get; set; }

    [JsonProperty("synchronous")]
    public bool Synchronous { get; set; }
}
