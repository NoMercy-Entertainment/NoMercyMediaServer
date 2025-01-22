using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.Seasons;

public class SeasonRepository(MediaContext context) : ISeasonRepository
{
    public Task StoreAsync(IEnumerable<Season> seasons)
    {
        return context.Seasons.UpsertRange(seasons.ToArray())
            .On(s => new { s.Id })
            .WhenMatched((ss, si) => new()
            {
                Id = si.Id,
                Title = si.Title,
                AirDate = si.AirDate,
                EpisodeCount = si.EpisodeCount,
                Overview = si.Overview,
                Poster = si.Poster,
                SeasonNumber = si.SeasonNumber,
                TvId = si.TvId,
                _colorPalette = si._colorPalette
            })
            .RunAsync();
    }

    public Task StoreTranslationsAsync(IEnumerable<Translation> translations)
    {
        return context.Translations.UpsertRange(translations.ToArray())
            .On(t => new { t.Iso31661, t.Iso6391, t.SeasonId })
            .WhenMatched((ts, ti) => new()
            {
                Iso31661 = ti.Iso31661,
                Iso6391 = ti.Iso6391,
                Name = ti.Name,
                EnglishName = ti.EnglishName,
                Title = ti.Title,
                Overview = ti.Overview,
                Homepage = ti.Homepage,
                Biography = ti.Biography,
                TvId = ti.TvId,
                SeasonId = ti.SeasonId,
                EpisodeId = ti.EpisodeId,
                MovieId = ti.MovieId,
                CollectionId = ti.CollectionId,
                PersonId = ti.PersonId,
                UpdatedAt = ti.UpdatedAt
            })
            .RunAsync();
    }

    public Task StoreImagesAsync(IEnumerable<Image> images)
    {
        return context.Images.UpsertRange(images.ToArray())
            .On(v => new { v.FilePath, v.SeasonId })
            .WhenMatched((ts, ti) => new()
            {
                AspectRatio = ti.AspectRatio,
                FilePath = ti.FilePath,
                Height = ti.Height,
                Iso6391 = ti.Iso6391,
                Site = ti.Site,
                VoteAverage = ti.VoteAverage,
                VoteCount = ti.VoteCount,
                Width = ti.Width,
                Type = ti.Type,
                SeasonId = ti.SeasonId,
                UpdatedAt = ti.UpdatedAt
            })
            .RunAsync();
    }

    public async Task<bool> RemoveSeasonAsync(int seasonId)
    {
        Season? season = await context.Seasons
            .FirstOrDefaultAsync(s => s.Id == seasonId);

        if (season is null) return false;

        context.Seasons.Remove(season);
        await context.SaveChangesAsync();

        return true;
    }
}