using Newtonsoft.Json;

namespace NoMercy.Server.Seeds.Dto;

public class FolderSeedDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
}
