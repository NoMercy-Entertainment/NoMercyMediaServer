
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(EncoderProfileId), nameof(FolderId))]
[Index(nameof(EncoderProfileId))]
[Index(nameof(FolderId))]
public class EncoderProfileFolder
{
    [JsonProperty("encoder_profile_id")] public Ulid EncoderProfileId { get; set; }
    public EncoderProfile EncoderProfile { get; set; } = null!;

    [JsonProperty("folder_id")] public Ulid FolderId { get; set; }
    public Folder Folder { get; set; } = null!;

    public EncoderProfileFolder()
    {
    }

    public EncoderProfileFolder(Ulid encoderProfileId, Ulid libraryId)
    {
        EncoderProfileId = encoderProfileId;
        FolderId = libraryId;
    }
}
