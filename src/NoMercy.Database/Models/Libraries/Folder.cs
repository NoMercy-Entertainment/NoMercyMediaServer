using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database.Internal;
using NoMercy.Database.Models.Storage;

namespace NoMercy.Database.Models.Libraries;

[PrimaryKey(nameof(Id))]
[Index(nameof(DriverId), nameof(Path), IsUnique = true)]
public class Folder
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; set; }

    private string _path = string.Empty;

    [JsonProperty("path")]
    public string Path
    {
        get => _path;
        set => _path = PathNormalizer.Normalize(value);
    }

    [JsonProperty("driver_id")]
    public Ulid DriverId { get; set; }

    [JsonProperty("driver")]
    public Driver? Driver { get; set; }

    [JsonProperty("encoder_profile_folder")]
    public ICollection<EncoderProfileFolder> EncoderProfileFolder { get; set; } = [];

    [JsonProperty("folder_libraries")]
    public ICollection<FolderLibrary> FolderLibraries { get; set; } = [];
}
