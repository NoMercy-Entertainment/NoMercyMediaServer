using Newtonsoft.Json;

namespace NoMercy.Server.Seeds.Dto;

public class FolderDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
}