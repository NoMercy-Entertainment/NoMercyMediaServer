using Newtonsoft.Json;
using NoMercy.Data.Logic;
using NoMercy.Database.Models;

namespace NoMercy.Data.Repositories;

public class FolderDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("path")] public string Path { get; set; } = string.Empty;
    [JsonProperty("encoder_profiles")] public EncoderProfileDto[] EncoderProfiles { get; set; } = [];

    public FolderDto()
    {
        
    }
    
    public FolderDto(Folder folder)
    {
        Id = folder.Id;
        Path = folder.Path;
        EncoderProfiles = folder.EncoderProfileFolder
            .Select(f => new EncoderProfileDto(f.EncoderProfile))
            .ToArray();
    }
}