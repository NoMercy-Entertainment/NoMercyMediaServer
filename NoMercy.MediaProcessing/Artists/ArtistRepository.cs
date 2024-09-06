using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.Artists;

public class ArtistRepository(MediaContext context) : IArtistRepository
{
    public Task StoreAsync(Artist artist)
    {
        return context.Artists.Upsert(artist)
            .On(e => new { e.Id })
            .WhenMatched((s, i) => new Artist
            {
                UpdatedAt = DateTime.UtcNow,
                Id = i.Id,
                Name = i.Name,
                Disambiguation = i.Disambiguation,
                Description = i.Description,

                Folder = i.Folder,
                HostFolder = i.HostFolder,
                LibraryId = i.LibraryId,
                FolderId = i.FolderId
            })
            .RunAsync();
    }

    public Task LinkToLibrary(ArtistLibrary artistLibrary)
    {
        return context.ArtistLibrary.Upsert(artistLibrary)
            .On(e => new { e.ArtistId, e.LibraryId })
            .WhenMatched((s, i) => new ArtistLibrary
            {
                ArtistId = i.ArtistId,
                LibraryId = i.LibraryId
            })
            .RunAsync();
    }

    public Task LinkToReleaseGroup(ArtistReleaseGroup artistReleaseGroup)
    {
        return context.ArtistReleaseGroup.Upsert(artistReleaseGroup)
            .On(e => new { e.ArtistId, e.ReleaseGroupId })
            .WhenMatched((s, i) => new ArtistReleaseGroup
            {
                ArtistId = i.ArtistId,
                ReleaseGroupId = i.ReleaseGroupId
            })
            .RunAsync();
    }

    public Task LinkToRelease(AlbumArtist artistRelease)
    {
        return context.AlbumArtist.Upsert(artistRelease)
            .On(e => new { e.AlbumId, e.ArtistId })
            .WhenMatched((s, i) => new AlbumArtist
            {
                AlbumId = i.AlbumId,
                ArtistId = i.ArtistId
            })
            .RunAsync();
    }

    public Task LinkToRecording(ArtistTrack artistRecording)
    {
        return context.ArtistTrack.Upsert(artistRecording)
            .On(e => new { e.ArtistId, e.TrackId })
            .WhenMatched((s, i) => new ArtistTrack
            {
                ArtistId = i.ArtistId,
                TrackId = i.TrackId
            })
            .RunAsync();
    }
}