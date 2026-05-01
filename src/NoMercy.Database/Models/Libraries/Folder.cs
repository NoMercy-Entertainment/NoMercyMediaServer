using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Libraries;

[PrimaryKey(nameof(Id))]
[Index(nameof(Path), IsUnique = true)]
[Index(nameof(DriverType))]
public class Folder
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("driver_type")]
    public string DriverType { get; set; } = "local";

    [JsonProperty("driver_config")]
    public string? DriverConfig { get; set; }

    [JsonProperty("encoder_profile_folder")]
    public ICollection<EncoderProfileFolder> EncoderProfileFolder { get; set; } = [];

    [JsonProperty("folder_libraries")]
    public ICollection<FolderLibrary> FolderLibraries { get; set; } = [];
}
