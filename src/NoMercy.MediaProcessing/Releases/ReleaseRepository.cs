using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.MediaProcessing.Releases;

public class ReleaseRepository(MediaContext context) : IReleaseRepository
{
    public Task Store(Album release)
    {
        return context.Albums.Upsert(release)
            .On(e => new { e.Id })
            .WhenMatched((s, i) => new()
            {
                Id = i.Id,
                Name = i.Name,
                Disambiguation = i.Disambiguation,
                Description = i.Description,
                Year = i.Year,
                Country = i.Country,
                Tracks = i.Tracks,
                _colorPalette = i._colorPalette,
                LibraryId = i.LibraryId,
                Folder = i.Folder,
                FolderId = i.FolderId,
                HostFolder = i.HostFolder,
                Cover = i.Cover,
            })
            .RunAsync();
    }

    public Task LinkToReleaseGroup(AlbumReleaseGroup albumReleaseGroup)
    {
        return context.AlbumReleaseGroup.Upsert(albumReleaseGroup)
            .On(e => new { e.AlbumId, e.ReleaseGroupId })
            .WhenMatched((s, i) => new()
            {
                AlbumId = i.AlbumId,
                ReleaseGroupId = i.ReleaseGroupId
            })
            .RunAsync();
    }

    public Task LinkToLibrary(AlbumLibrary albumLibrary)
    {
        return context.AlbumLibrary.Upsert(albumLibrary)
            .On(e => new { e.AlbumId, e.LibraryId })
            .WhenMatched((s, i) => new()
            {
                AlbumId = i.AlbumId,
                LibraryId = i.LibraryId
            })
            .RunAsync();
    }
}