using Newtonsoft.Json;
using NoMercy.Data.Logic;
using NoMercy.Database.Models.Libraries;

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
            .Where(f => f.EncoderProfile is not null)
            .Select(f => new EncoderProfileDto
            {
                Id = f.EncoderProfile.Id,
                Name = f.EncoderProfile.Name,
                Container = f.EncoderProfile.Container ?? string.Empty
            })
            .ToArray();
    }
}