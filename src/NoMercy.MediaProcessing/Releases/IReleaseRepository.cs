using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.Releases;

public interface IReleaseRepository
{
    public Task Store(Album release);
    public Task LinkToLibrary(AlbumLibrary albumLibrary);
    public Task LinkToReleaseGroup(AlbumReleaseGroup albumReleaseGroup);
}