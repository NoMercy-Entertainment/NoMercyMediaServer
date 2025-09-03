using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Images;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.FanArt.Models;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;
using Image = NoMercy.Database.Models.Image;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class ProcessFanartArtistImagesCronJob : ICronJobExecutor
{
    private readonly ILogger<ProcessFanartArtistImagesCronJob> _logger;

    public string CronExpression => new CronExpressionBuilder().EveryHour();
    public string JobName => "Fanart ColorPalette Job";

    public ProcessFanartArtistImagesCronJob(ILogger<ProcessFanartArtistImagesCronJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        await using MediaContext context = new();

        List<Artist[]> artists = context.Artists
            .Include(artist => artist.AlbumArtist)
            .ThenInclude(albumArtist => albumArtist.Album)
            .Include(artist => artist.Images)
            
            .Where(x => (string.IsNullOrEmpty(x._colorPalette) && x.Cover != null) || 
                        !x.Images.Any(i => 
                            string.IsNullOrEmpty(i._colorPalette) && 
                            i.Site != null && 
                            i.Site.StartsWith("https://assets.fanart.tv"))
            )
            .OrderByDescending(artist => artist.UpdatedAt)
            
            .Take(200)
            .ToList()
            .Chunk(5)
            .ToList();
        
        _logger.LogTrace("Found {Count} artist chunks to process", artists.Count);

        foreach (Artist[] artistChunk in artists)
        {
            _logger.LogTrace("Processing artist chunk of size: {Size}", artistChunk.Length);
            
            foreach (Artist artist in artistChunk)
            {
                await ProcessImages(context, artist, cancellationToken);
            }
        }
            
        _logger.LogTrace("Fanart Artist images job completed, updated: {Count}", artists.Count);
    }

    private async Task ProcessImages(MediaContext context, Artist artist, CancellationToken cancellationToken)
    {
        ImageRepository imageRepository = new(context);
        FanArtImageManager imageManager = new(imageRepository, context);
        using FanArtMusicClient fanArtMusicClient = new();
        
        try 
        {
            FanArtArtistDetails? fanArt = await fanArtMusicClient.Artist(artist.Id);
            if (fanArt is null) return;
            
            artist.UpdatedAt = DateTime.Now;
            context.Artists.Update(artist);
            await context.SaveChangesAsync(cancellationToken);
            
            List<Image> releaseImages = await imageManager.StoreReleaseImages(fanArt.ArtistAlbum, artist.Id, artist);

            ICollection<Image> artistImages = await imageManager.StoreArtistImages(fanArt, artist.Id, artist);
                    
            List<Image> images = releaseImages
                .Concat(artistImages)
                .Where(image => string.IsNullOrEmpty(image._colorPalette))
                .ToList();
            
            if (images.Count == 0) return;

            foreach (Image image in images)
            {
                image._colorPalette = await FanArtImageManager.ColorPalette("image", new(image.Site + image.FilePath));
                image.UpdatedAt = DateTime.Now;
            }
                    
            await context.Images.UpsertRange(images.Where(i => i.ArtistId != null))
                .On(i => new { i.FilePath, i.ArtistId })
                .WhenMatched((db, src) => new()
                {
                    _colorPalette = src._colorPalette,
                    UpdatedAt = DateTime.Now
                })
                .RunAsync(cancellationToken);
                    
            await context.Images.UpsertRange(images.Where(i => i.AlbumId != null))
                .On(i => new { i.FilePath, i.AlbumId })
                .WhenMatched((db, src) => new()
                {
                    _colorPalette = src._colorPalette,
                    UpdatedAt = DateTime.Now
                })
                .RunAsync(cancellationToken);
                    
            Image? cover = releaseImages.FirstOrDefault(i => i.Type == "thumb");
                    
            if (cover == null) return;
                    
            artist.Cover = cover.FilePath;
            artist._colorPalette = cover._colorPalette.Replace("\"image\"", "\"cover\"");
            artist.UpdatedAt = DateTime.Now;
                    
            context.Artists.Update(artist);
                    
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            _logger.LogError(e, "Error while processing image {Id}", artist.Id);
        }
    }
}