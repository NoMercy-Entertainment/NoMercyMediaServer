using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.Images;

public class ImageRepository(MediaContext context): IImageRepository
{
    public Task StoreArtistImages(IEnumerable<Image> images, Artist dbArtist)
    {
        return context.Images.UpsertRange(images)
            .On(v => new { v.FilePath, v.ArtistId })
            .WhenMatched((s, i) => new()
            {
                Id = i.Id,
                AspectRatio = i.AspectRatio,
                Height = i.Height,
                FilePath = i.FilePath,
                Width = i.Width,
                VoteCount = i.VoteCount,
                ArtistId = i.ArtistId,
                Type = i.Type,
                Site = i.Site,
                _colorPalette = i._colorPalette,
                UpdatedAt = i.UpdatedAt
            })
            .RunAsync();
    }

    public Task StoreReleaseImages(IEnumerable<Image> images)
    {
        return context.Images.UpsertRange(images)
            .On(v => new { v.FilePath, v.AlbumId })
            .WhenMatched((s, i) => new()
            {
                Id = i.Id,
                AspectRatio = i.AspectRatio,
                Name = i.Name,
                Height = i.Height,
                FilePath = i.FilePath,
                Width = i.Width,
                VoteCount = i.VoteCount,
                AlbumId = i.AlbumId,
                Type = i.Type,
                Site = i.Site,
                _colorPalette = i._colorPalette,
                UpdatedAt = i.UpdatedAt
            })
            .RunAsync();
    }
    
    public Task<ReleaseGroup> GetReleaseImages(Guid id)
    {
        return context.ReleaseGroups
            .Include(a => a.AlbumReleaseGroup)
            .ThenInclude(a => a.Album)
            .FirstAsync(a => a.Id == id);
    }

    public Task CommitReleaseChanges()
    {
        return context.SaveChangesAsync();
    }
}