using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Images;
using NoMercy.Providers.FanArt.Client;
using NoMercy.Providers.FanArt.Models;
using NoMercy.Queue;
using NoMercy.Queue.Interfaces;
using Image = NoMercy.Database.Models.Media.Image;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

public class ArtistFanartCronJob : ICronJobExecutor
{
    private readonly ILogger<ArtistFanartCronJob> _logger;
    private readonly MediaContext _context;

    public string CronExpression => new CronExpressionBuilder().Daily();
    public string JobName => "Fanart ColorPalette Job";

    public ArtistFanartCronJob(ILogger<ArtistFanartCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        List<Image[]> imagesWithoutPalette = _context.Images
            .Where(i => string.IsNullOrEmpty(i._colorPalette) &&
                        i.Site != null &&
                        i.Site.StartsWith("https://assets.fanart.tv"))
            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} image chunks to process", imagesWithoutPalette.Count);

        foreach (Image[] imageChunk in imagesWithoutPalette)
        {
            _logger.LogTrace("Processing image chunk of size: {Size}", imageChunk.Length);

            foreach (Image image in imageChunk)
            {
                try
                {
                    image._colorPalette = await FanArtImageManager.ColorPalette("image", new(image.Site + image.FilePath));
                }
                catch (Exception)
                {
                    image._colorPalette = "{}";
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }


        _logger.LogTrace("Fanart Images job completed, updated: {Count}", imagesWithoutPalette.Count);

        List<Artist[]> artists = _context.Artists
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

            .Take(5000)
            .ToList()
            .Chunk(5)
            .ToList();

        _logger.LogTrace("Found {Count} artist chunks to process", artists.Count);

        foreach (Artist[] artistChunk in artists)
        {
            _logger.LogTrace("Processing artist chunk of size: {Size}", artistChunk.Length);

            foreach (Artist artist in artistChunk)
            {
                await ProcessImages(artist, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

        }

        _logger.LogTrace("Fanart Artist images job completed, updated: {Count}", artists.Count);
    }

    private async Task ProcessImages(Artist artist, CancellationToken cancellationToken)
    {
        ImageRepository imageRepository = new(_context);
        FanArtImageManager imageManager = new(imageRepository);
        using FanArtMusicClient fanArtMusicClient = new();

        try
        {
            FanArtArtistDetails? fanArt = await fanArtMusicClient.Artist(artist.Id);
            if (fanArt is null) return;

            List<Image> releaseImages = await imageManager.StoreReleaseImages(fanArt.ArtistAlbum, artist.Id, artist);

            ICollection<Image> artistImages = await imageManager.StoreArtistImages(fanArt, artist.Id, artist);

            List<Image> images = releaseImages
                .Concat(artistImages)
                .Where(image => string.IsNullOrEmpty(image._colorPalette))
                .ToList();

            if (images.Count == 0)
            {
                Image? artistCover = artistImages
                    .FirstOrDefault(i => !string.IsNullOrEmpty(i._colorPalette));

                string artistColorPalette = artistCover != null
                    ? artistCover._colorPalette.Replace("\"image\"", "\"cover\"")
                    : "{}";

                string coverPath = artistCover != null
                    ? artistCover.FilePath
                    : artist.Cover ?? string.Empty;

                await _context.Artists
                    .Where(a => a.Id == artist.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.Cover, coverPath)
                        .SetProperty(a => a._colorPalette, artistColorPalette),
                        cancellationToken);

                return;
            }

            foreach (Image image in images)
            {
                try
                {
                    image._colorPalette = await FanArtImageManager.ColorPalette("image", new(image.Site + image.FilePath));
                }
                catch (Exception)
                {
                    image._colorPalette = "{}";
                }
            }

            await _context.Images.UpsertRange(images.Where(i => i.ArtistId != null))
                .On(i => new { i.FilePath, i.ArtistId })
                .WhenMatched((db, src) => new()
                {
                    _colorPalette = src._colorPalette
                })
                .RunAsync(cancellationToken);

            await _context.Images.UpsertRange(images.Where(i => i.AlbumId != null))
                .On(i => new { i.FilePath, i.AlbumId })
                .WhenMatched((db, src) => new()
                {
                    _colorPalette = src._colorPalette
                })
                .RunAsync(cancellationToken);

            Image? cover = releaseImages.FirstOrDefault(i => i.Type == "thumb");

            if (cover == null) return;

            artist.Cover = cover.FilePath;
            artist._colorPalette = cover._colorPalette.Replace("\"image\"", "\"cover\"");
        }
        catch (Exception e)
        {
            if (e.Message.Contains("404")) return;
            _logger.LogError(e, "Error while processing image {Id}: {Err}", artist.Id, e);
        }
    }
}
