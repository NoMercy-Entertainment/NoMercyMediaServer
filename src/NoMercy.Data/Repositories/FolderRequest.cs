using Newtonsoft.Json;

namespace NoMercy.Data.Repositories;

public class FolderRequest
{
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
}