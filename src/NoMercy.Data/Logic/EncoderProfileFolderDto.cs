using Newtonsoft.Json;
using NoMercy.Data.Repositories;
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