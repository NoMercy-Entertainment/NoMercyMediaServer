using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Data.Repositories;

public class TvShowRepository(MediaContext context)
{
    public readonly Func<MediaContext, Guid, int, string, string, Task<Tv?>> GetTvAsync =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, int id, string language, string country) =>
            mediaContext.Tvs.AsNoTracking()
                .Where(tv => tv.Id == id)
                .Where(tv => tv.Library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
                .Include(tv => tv.TvUser)
                .Include(tv => tv.Library)
                .ThenInclude(library => library.LibraryUsers)
                .Include(tv => tv.Media)
                .Include(tv => tv.AlternativeTitles)
                .Include(tv => tv.Translations
                    .Where(translation => translation.Iso6391 == language))
                .Include(tv => tv.Images
                    .Where(image =>
                        (image.Type == "logo" && image.Iso6391 == "en")
                        || ((image.Type == "backdrop" || image.Type == "poster") &&
                            (image.Iso6391 == "en" || image.Iso6391 == null))
                    )
                    .OrderByDescending(image => image.VoteAverage)
                )
                .Include(tv => tv.CertificationTvs
                    .Where(certification => certification.Certification.Iso31661 == country ||
                                            certification.Certification.Iso31661 == "US"))
                .ThenInclude(certificationTv => certificationTv.Certification)
                .Include(tv => tv.Creators)
                .ThenInclude(genreTv => genreTv.Person)
                .Include(tv => tv.GenreTvs)
                .ThenInclude(genreTv => genreTv.Genre)
                .Include(tv => tv.KeywordTvs)
                .ThenInclude(keywordTv => keywordTv.Keyword)
                .Include(tv => tv.Cast)
                .ThenInclude(castTv => castTv.Person)
                .Include(tv => tv.Cast)
                .ThenInclude(castTv => castTv.Role)
                .Include(tv => tv.Crew)
                .ThenInclude(crewTv => crewTv.Person)
                .Include(tv => tv.Crew)
                .ThenInclude(crewTv => crewTv.Job)
                .Include(tv => tv.Seasons)
                .ThenInclude(season => season.Translations
                    .Where(translation => translation.Iso6391 == language)
                )
                .Include(tv => tv.Seasons)
                .ThenInclude(season => season.Episodes)
                .ThenInclude(episode => episode.Translations
                    .Where(translation => translation.Iso6391 == language)
                )
                .Include(tv => tv.Seasons)
                .ThenInclude(season => season.Episodes)
                .ThenInclude(episode => episode.VideoFiles)
                .ThenInclude(file => file.UserData.Where(
                    userData => userData.UserId.Equals(userId))
                )
                .Include(tv => tv.Episodes)
                .ThenInclude(episode => episode.VideoFiles)
                .ThenInclude(file => file.UserData.Where(
                    userData => userData.UserId.Equals(userId)))
                .Include(tv => tv.RecommendationFrom)
                .Include(tv => tv.SimilarFrom)
                .Include(tv => tv.Episodes)
                .ThenInclude(episode => episode.Cast)
                .ThenInclude(castTv => castTv.Person)
                .Include(tv => tv.Episodes)
                .ThenInclude(episode => episode.Cast)
                .ThenInclude(castTv => castTv.Role)
                .Include(tv => tv.Episodes)
                .ThenInclude(episode => episode.Crew)
                .ThenInclude(crewTv => crewTv.Person)
                .Include(tv => tv.Episodes)
                .ThenInclude(episode => episode.Crew)
                .ThenInclude(crewTv => crewTv.Job)
                .FirstOrDefault());
    
    public Task<bool> GetTvAvailableAsync(Guid userId, int id)
    {
        return context.Tvs.AsNoTracking()
            .Where(tv => tv.Library.LibraryUsers
                .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
            .Where(tv => tv.Id == id)
            .Include(tv => tv.Episodes)
            .ThenInclude(tv => tv.VideoFiles)
            .AnyAsync(tv => tv.Episodes
                .Any(episode => episode.VideoFiles.Any()));
    }

    public async Task<Tv?> GetTvPlaylistAsync(Guid userId, int id, string language)
    {
        return await context.Tvs.AsNoTracking()
            .Where(tv => tv.Id == id)
            .Where(tv => tv.Library.LibraryUsers
                .FirstOrDefault(u => u.UserId.Equals(userId)) != null)
            .Include(tv => tv.Seasons.OrderBy(season => season.SeasonNumber))
            .ThenInclude(season => season.Episodes.OrderBy(episode => episode.EpisodeNumber))
            .Include(tv => tv.Translations
                .Where(translation => translation.Iso6391 == language))
            .Include(tv => tv.Seasons)
            .ThenInclude(season => season.Episodes)
            .ThenInclude(tv => tv.Tv)
            .ThenInclude(tv => tv.Translations
                .Where(translation => translation.Iso6391 == language))
            .Include(tv => tv.Seasons)
            .ThenInclude(season => season.Episodes)
            .ThenInclude(tv => tv.Tv)
            .ThenInclude(tv => tv.Media
                .Where(media => media.Type == "video"))
            .Include(tv => tv.Seasons)
            .ThenInclude(season => season.Episodes)
            .ThenInclude(tv => tv.Tv)
            .ThenInclude(tv => tv.Images
                .Where(image => image.Type == "logo"))
            .Include(tv => tv.Seasons)
            .ThenInclude(season => season.Episodes)
            .ThenInclude(tv => tv.VideoFiles)
            .ThenInclude(file => file.UserData.Where(
                userData => userData.UserId.Equals(userId)))
            .Include(tv => tv.Seasons)
            .ThenInclude(season => season.Translations
                .Where(translation => translation.Iso6391 == language))
            .Include(tv => tv.Seasons)
            .ThenInclude(season => season.Episodes)
            .ThenInclude(episode => episode.Translations
                .Where(translation => translation.Iso6391 == language)
            )
            .FirstOrDefaultAsync();
    }

    public async Task<bool> LikeTvAsync(int id, Guid userId, bool like)
    {
        TvUser? tvUser = await context.TvUser
            .FirstOrDefaultAsync(tu => tu.TvId == id && tu.UserId.Equals(userId));

        if (like)
        {
            await context.TvUser.Upsert(new(id, userId))
                .On(m => new { m.TvId, m.UserId })
                .WhenMatched(m => new()
                {
                    TvId = m.TvId,
                    UserId = m.UserId
                })
                .RunAsync();
        }
        else if (tvUser != null)
        {
            context.TvUser.Remove(tvUser);

            await context.SaveChangesAsync();
        }

        return true;
    }

    public async Task AddTvShowAsync(int id)
    {
        Library? tvLibrary = await context.Libraries
            .Where(f => f.Type == "tv")
            .FirstOrDefaultAsync();

        if (tvLibrary == null) return;

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AddShowJob>(id, tvLibrary);
    }

    public Task DeleteTvAsync(int id)
    {
        return context.Tvs
            .Where(tv => tv.Id == id)
            .ExecuteDeleteAsync();
    }
}