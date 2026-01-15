using Newtonsoft.Json;

namespace NoMercy.Networking.Dto;

public class RefreshLibraryDto
{
    [JsonProperty("queryKey")] public dynamic?[] QueryKey { get; set; } = [];
}