using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Database.Models.Media;

[PrimaryKey(nameof(PresetId), nameof(FolderId))]
[Index(nameof(FolderId))]
[Index(nameof(PresetId), nameof(IsDefault))]
public class EncodingPresetFolder
{
    [JsonProperty("preset_id")]
    public Ulid PresetId { get; set; }

    [JsonProperty("folder_id")]
    public Ulid FolderId { get; set; }

    [JsonProperty("is_default")]
    public bool IsDefault { get; set; }

    public EncodingPreset? Preset { get; set; }
    public Folder? Folder { get; set; }
}
