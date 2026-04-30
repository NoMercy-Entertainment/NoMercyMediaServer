using Newtonsoft.Json;
using NoMercy.NmSystem.Dto;

namespace NoMercy.Api.DTOs.Dashboard;

public record DirectoryTreeListing
{
    [JsonProperty("status")]
    public string Status { get; set; } = "ok";

    [JsonProperty("path", NullValueHandling = NullValueHandling.Ignore)]
    public string? Path { get; set; }

    [JsonProperty("parent")]
    public string? Parent { get; set; }

    [JsonProperty("data")]
    public List<DirectoryTree> Data { get; set; } = [];
}
