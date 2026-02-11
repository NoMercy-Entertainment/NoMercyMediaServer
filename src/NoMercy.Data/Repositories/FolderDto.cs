using Newtonsoft.Json;
using NoMercy.Data.Logic;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

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