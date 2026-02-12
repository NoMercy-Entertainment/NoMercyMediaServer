using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;

namespace NoMercy.MediaProcessing.Images;

public class ImageRepository(MediaContext context) : IImageRepository
{
    public async Task<ICollection<Image>> StoreArtistImages(IEnumerable<Image> images, Artist dbArtist)
    {
        return await context.Images.UpsertRange(images)
            .On(v => new { v.FilePath, v.ArtistId })
            .WhenMatched((s, i) => new()
            {
                AspectRatio = i.AspectRatio,
                Height = i.Height,
                FilePath = i.FilePath,
                Width = i.Width,
                VoteCount = i.VoteCount,
                ArtistId = i.ArtistId,
                Type = i.Type,
                Site = i.Site
            })
            .RunAndReturnAsync();
    }

    public async Task<ICollection<Image>> StoreReleaseImages(IEnumerable<Image> images)
    {
        return await context.Images.UpsertRange(images)
            .On(v => new { v.FilePath, v.AlbumId })
            .WhenMatched((s, i) => new()
            {
                AspectRatio = i.AspectRatio,
                Name = i.Name,
                Height = i.Height,
                FilePath = i.FilePath,
                Width = i.Width,
                VoteCount = i.VoteCount,
                AlbumId = i.AlbumId,
                Type = i.Type,
                Site = i.Site
            })
            .RunAndReturnAsync();
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