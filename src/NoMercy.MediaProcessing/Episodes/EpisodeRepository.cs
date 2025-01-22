using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.Episodes;

public class EpisodeRepository(MediaContext context) : IEpisodeRepository
{
    public Task StoreEpisodes(IEnumerable<Episode> episodes)
    {
        return context.Episodes.UpsertRange(episodes.ToArray())
            .On(e => new { e.Id })
            .WhenMatched((es, ei) => new()
            {
                Id = ei.Id,
                Title = ei.Title,
                AirDate = ei.AirDate,
                EpisodeNumber = ei.EpisodeNumber,
                Overview = ei.Overview,
                ProductionCode = ei.ProductionCode,
                SeasonNumber = ei.SeasonNumber,
                Still = ei.Still,
                TvId = ei.TvId,
                SeasonId = ei.SeasonId,
                _colorPalette = ei._colorPalette
            })
            .RunAsync();
    }

    public Task StoreEpisodeTranslations(IEnumerable<Translation> translations)
    {
        return context.Translations.UpsertRange(translations.ToArray())
            .On(t => new { t.Iso31661, t.Iso6391, t.EpisodeId })
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

    public Task StoreEpisodeImages(IEnumerable<Image> images)
    {
        return context.Images.UpsertRange(images.ToArray())
            .On(v => new { v.FilePath, v.EpisodeId })
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
                EpisodeId = ti.EpisodeId,
                UpdatedAt =ti.UpdatedAt
            })
            .RunAsync();
    }
}