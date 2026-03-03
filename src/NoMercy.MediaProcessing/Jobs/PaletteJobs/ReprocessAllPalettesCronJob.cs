using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Information;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NoMercy.MediaProcessing.Jobs.PaletteJobs;

/// <summary>
/// Temporary one-shot job to regenerate all color palettes using the new Median Cut algorithm.
/// Runs once per minute, processes a batch per entity type each tick, and self-stops when done.
/// Remove this job and its registration after all palettes are regenerated.
/// </summary>
public class ReprocessAllPalettesCronJob : ICronJobExecutor
{
    private readonly ILogger<ReprocessAllPalettesCronJob> _logger;
    private readonly MediaContext _context;

    private const int BatchSize = 50;
    private const int MaxParallelism = 5;

    public string CronExpression => new CronExpressionBuilder().EveryMinutes(1);
    public string JobName => "Reprocess All ColorPalettes Job";

    public ReprocessAllPalettesCronJob(ILogger<ReprocessAllPalettesCronJob> logger, MediaContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        int totalProcessed = 0;

        totalProcessed += await ProcessMovies(cancellationToken);
        totalProcessed += await ProcessShows(cancellationToken);
        totalProcessed += await ProcessSeasons(cancellationToken);
        totalProcessed += await ProcessEpisodes(cancellationToken);
        totalProcessed += await ProcessCollections(cancellationToken);
        totalProcessed += await ProcessPeople(cancellationToken);
        totalProcessed += await ProcessRecommendations(cancellationToken);
        totalProcessed += await ProcessSimilar(cancellationToken);
        totalProcessed += await ProcessImages(cancellationToken);
        totalProcessed += await ProcessArtists(cancellationToken);
        totalProcessed += await ProcessAlbums(cancellationToken);

        if (totalProcessed == 0)
        {
            _logger.LogInformation("Reprocess all palettes: no items remaining — job complete");
        }
        else
        {
            _logger.LogInformation("Reprocess all palettes: processed {Count} items this tick", totalProcessed);
        }
    }

    private async Task<int> ProcessMovies(CancellationToken ct)
    {
        List<Movie> items = _context.Movies
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .Where(x => x.Poster != null || x.Backdrop != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await MovieDbImageManager.MultiColorPalette([
                    new("poster", item.Poster),
                    new("backdrop", item.Backdrop)
                ]);
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} movies", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessShows(CancellationToken ct)
    {
        List<Tv> items = _context.Tvs
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .Where(x => x.Poster != null || x.Backdrop != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await MovieDbImageManager.MultiColorPalette([
                    new("poster", item.Poster),
                    new("backdrop", item.Backdrop)
                ]);
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} shows", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessSeasons(CancellationToken ct)
    {
        List<Season> items = _context.Seasons
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .Where(x => x.Poster != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await MovieDbImageManager.ColorPalette("poster", item.Poster);
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} seasons", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessEpisodes(CancellationToken ct)
    {
        List<Episode> items = _context.Episodes
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .Where(x => x.Still != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await MovieDbImageManager.ColorPalette("still", item.Still);
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} episodes", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessCollections(CancellationToken ct)
    {
        List<Collection> items = _context.Collections
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .Where(x => x.Poster != null || x.Backdrop != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await MovieDbImageManager.MultiColorPalette([
                    new("poster", item.Poster),
                    new("backdrop", item.Backdrop)
                ]);
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} collections", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessPeople(CancellationToken ct)
    {
        List<Person> items = _context.People
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .Where(x => x.Profile != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await MovieDbImageManager.ColorPalette("profile", item.Profile);
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} people", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessRecommendations(CancellationToken ct)
    {
        List<Recommendation> items = _context.Recommendations
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .Where(x => x.Poster != null || x.Backdrop != null)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await MovieDbImageManager.MultiColorPalette([
                    new("poster", item.Poster),
                    new("backdrop", item.Backdrop)
                ]);
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} recommendations", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessSimilar(CancellationToken ct)
    {
        List<Similar> items = _context.Similar
            .Where(x => string.IsNullOrEmpty(x._colorPalette))
            .Where(x => x.Poster != null || x.Backdrop != null)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await MovieDbImageManager.MultiColorPalette([
                    new("poster", item.Poster),
                    new("backdrop", item.Backdrop)
                ]);
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} similar", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessImages(CancellationToken ct)
    {
        List<NoMercy.Database.Models.Media.Image> items = _context.Images
            .Where(i => i.Site == "https://image.tmdb.org/t/p/")
            .Where(x => string.IsNullOrEmpty(x._colorPalette) && !x.FilePath.EndsWith(".svg"))
            .Where(e => e.Iso6391 == null || e.Iso6391 == "en" || e.Iso6391 == "" ||
                        e.Iso6391 == CultureInfo.CurrentCulture.TwoLetterISOLanguageName)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await MovieDbImageManager.ColorPalette("image", item.FilePath);
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} images", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessArtists(CancellationToken ct)
    {
        List<Artist> items = _context.Artists
            .Where(x => string.IsNullOrEmpty(x._colorPalette) && x.Cover != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                string filePath = AppFiles.MusicImagesPath + item.Cover;
                if (File.Exists(filePath))
                {
                    using Image<Rgba32> image = await Image.LoadAsync<Rgba32>(filePath);
                    item._colorPalette = BaseImageManager.GenerateColorPalette([
                        new() { Key = "cover", ImageData = image }
                    ]);
                }
                else
                {
                    item._colorPalette = "{}";
                }
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} artists", items.Count);
        return items.Count;
    }

    private async Task<int> ProcessAlbums(CancellationToken ct)
    {
        List<Album> items = _context.Albums
            .Where(x => string.IsNullOrEmpty(x._colorPalette) && x.Cover != null)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(BatchSize)
            .ToList();

        if (items.Count == 0) return 0;

        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct }, async (item, _) =>
        {
            try
            {
                item._colorPalette = await CoverArtImageManagerManager
                    .ColorPalette("cover", new(AppFiles.MusicImagesPath + item.Cover));
            }
            catch (Exception)
            {
                item._colorPalette = "{}";
            }
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogTrace("Reprocess palettes: {Count} albums", items.Count);
        return items.Count;
    }
}
