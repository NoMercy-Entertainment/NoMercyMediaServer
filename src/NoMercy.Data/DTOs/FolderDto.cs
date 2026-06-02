using Newtonsoft.Json;
using NoMercy.Data.Logic;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Data.DTOs;

public class FolderDto
{
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("driver_id")]
    public Ulid DriverId { get; set; }

    [JsonProperty("driver_name")]
    public string DriverName { get; set; } = string.Empty;

    [JsonProperty("encoder_profiles")]
    public EncoderProfileDto[] EncoderProfiles { get; set; } = [];

    public FolderDto() { }

    public FolderDto(Folder folder)
    {
        Id = folder.Id;
        Path = folder.Path;
        DriverId = folder.DriverId;
        DriverName = folder.Driver?.Name ?? string.Empty;
        EncoderProfiles = folder
            .EncoderProfileFolder.Where(f => f.EncoderProfile is not null)
            .Select(f => new EncoderProfileDto
            {
                Id = f.EncoderProfile.Id,
                Name = f.EncoderProfile.Name,
                Container = f.EncoderProfile.Container.OrEmpty(),
            })
            .ToArray();
    }
}
