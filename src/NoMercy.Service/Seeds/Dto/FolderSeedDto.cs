using Newtonsoft.Json;

namespace NoMercy.Service.Seeds.Dto;

public class FolderSeedDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
}
