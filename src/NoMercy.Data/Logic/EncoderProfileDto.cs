using Newtonsoft.Json;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Data.Logic;

public class EncoderProfileDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("container")] public string Container { get; set; } = string.Empty;
    [JsonProperty("params")] public EncoderProfileParamsDto Params { get; set; } = new();

    [JsonProperty("encoder_profile_folder")]
    public List<EncoderProfileFolderDto> EncoderProfileFolder { get; set; } = [];

    public EncoderProfileDto()
    {
        
    }
    
    public EncoderProfileDto(EncoderProfile argEncoderProfile)
    {
        Id = argEncoderProfile.Id;
        Name = argEncoderProfile.Name;
        Container = argEncoderProfile.Container ?? string.Empty;
        Params = new(argEncoderProfile);
        EncoderProfileFolder = argEncoderProfile.EncoderProfileFolder
            .Select(ef => new EncoderProfileFolderDto(ef))
            .ToList();
    }

}
