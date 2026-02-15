using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;

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
            })
            .RunAsync();
    }

    public Task StoreEpisodeTranslations(List<Translation> translations)
    {
        int[] episodeIds = context.Episodes
            .Select(e => e.Id)
            .ToArray()
            .Where(e => translations.Any(t => e == t.EpisodeId))
            .ToArray();
        
        translations = translations
            .Where(t => t.EpisodeId is not null && episodeIds.Contains(t.EpisodeId.Value))
            .ToList();
        
        return context.Translations.UpsertRange(translations)
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
                PersonId = ti.PersonId
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
                EpisodeId = ti.EpisodeId
            })
            .RunAsync();
    }
}