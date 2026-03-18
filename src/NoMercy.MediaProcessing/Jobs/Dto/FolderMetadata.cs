using NoMercy.NmSystem.Dto;
using NoMercy.Providers.MusicBrainz.Models;

namespace NoMercy.MediaProcessing.Jobs.Dto;

public record FolderMetadata
{
    public MusicBrainzReleaseAppends MusicBrainzRelease { get; set; } = null!;
    public string BasePath { get; set; } = string.Empty;
    public List<MediaFile> Files { get; set; } = [];
    public string ArtistName { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string FolderReleaseName { get; set; } = string.Empty;
    public string FolderStartLetter { get; set; } = string.Empty;
}