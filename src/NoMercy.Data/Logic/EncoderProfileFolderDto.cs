using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models;

namespace NoMercy.Data.Logic;

public class EncoderProfileFolderDto
{
    [JsonProperty("encoder_profile_id")] public Ulid EncoderProfileId { get; set; }
    [JsonProperty("folder_id")] public Ulid FolderId { get; set; }
    [JsonProperty("folder")] public FolderLibraryDto FolderLibrary { get; set; } = new();

    public EncoderProfileFolderDto()
    {
        
    }
    
    public EncoderProfileFolderDto(EncoderProfileFolder encoderProfileFolder)
    {
        EncoderProfileId = encoderProfileFolder.EncoderProfileId;
        FolderId = encoderProfileFolder.FolderId;
        FolderLibrary = new(encoderProfileFolder.Folder.FolderLibraries);
    }

}