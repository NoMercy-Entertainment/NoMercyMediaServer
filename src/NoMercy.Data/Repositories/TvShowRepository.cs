using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.Other;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.TV;

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
                .Include(tv => tv.Media
                    .Where(media => media.Type == "Trailer"))
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
                    .ThenInclude(file => file.UserData.Where(userData => userData.UserId.Equals(userId))
                    )
                .Include(tv => tv.Episodes)
                    .ThenInclude(episode => episode.VideoFiles)
                    .ThenInclude(file => file.UserData.Where(userData => userData.UserId.Equals(userId)))
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
                .Include(tv => tv.WatchProviderMedia
                    .Where(wpm => wpm.CountryCode == country))
                    .ThenInclude(wpm => wpm.WatchProvider)
                .Include(tv => tv.NetworkTvs)
                    .ThenInclude(ntv => ntv.Network)
                .Include(tv => tv.CompaniesTvs)
                    .ThenInclude(ctv => ctv.Company)
                .FirstOrDefault());

    public Task<bool> GetTvAvailableAsync(Guid userId, int id)
    {
        return context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Where(tv => tv.Id == id)
            .AnyAsync(tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)));
    }

    public async Task<Tv?> GetTvPlaylistAsync(Guid userId, int id, string language, string country)
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
                    .Where(image => image.Type == "logo" && image.Iso6391 == "en" && image.Width > image.Height))
            .Include(tv => tv.Seasons)
                .ThenInclude(season => season.Episodes)
                .ThenInclude(tv => tv.VideoFiles)
                .ThenInclude(videoFile => videoFile.Metadata)
            .Include(tv => tv.Seasons)
                .ThenInclude(season => season.Episodes)
                .ThenInclude(tv => tv.VideoFiles)
                .ThenInclude(file => file.UserData
                    .Where(userData => userData.UserId.Equals(userId) && userData.Type == "tv"))
            .Include(tv => tv.Seasons)
                .ThenInclude(season => season.Translations
                    .Where(translation => translation.Iso6391 == language))
            .Include(tv => tv.Seasons)
                .ThenInclude(season => season.Episodes)
                .ThenInclude(episode => episode.Translations
                    .Where(translation => translation.Iso6391 == language)
                )
            
            .Include(tv => tv.Seasons)
            .ThenInclude(season => season.Episodes)
            .ThenInclude(tv => tv.Tv)
            .ThenInclude(tv => tv.CertificationTvs
                .Where(certification => certification.Certification.Iso31661 == country ||
                                        certification.Certification.Iso31661 == "US"))
            .ThenInclude(certificationTv => certificationTv.Certification)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> LikeTvAsync(int id, Guid userId, bool like)
    {
        TvUser? tvUser = await context.TvUser
            .FirstOrDefaultAsync(tu => tu.TvId == id && tu.UserId == userId);

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
        TmdbTvClient tvClient = new(id);
        TmdbTvShowDetails? show = await tvClient.Details(true);
        if (show == null) return;

        bool isAnime = KitsuIo.IsAnime(show.Name, show.FirstAirDate.ParseYear()).Result;

        Library? tvLibrary = await context.Libraries
            .Where(f => f.Type == (isAnime ? "anime" : "tv"))
            .FirstOrDefaultAsync() ?? await context.Libraries
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

    public async Task<IEnumerable<Episode>> GetMissingLibraryShows(Guid userId, int id, string language)
    {
        Tv? tv = await context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .Where(tv => tv.Library.LibraryUsers.Any(u => u.UserId == userId))
            .Include(tv => tv.Episodes.Where(e => e.VideoFiles.Count == 0))
                .ThenInclude(e => e.Translations.Where(t => t.Iso6391 == language))
            .FirstOrDefaultAsync();

        if (tv == null)
            return [];

        return tv.Episodes.Where(e => e.Translations.Any());
    }
    
    public async Task<bool> AddToWatchListAsync(int tvId, Guid userId, bool add = true)
    {
        Tv? tv = await context.Tvs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tvId);
    
        if (tv is null)
            return false;
    
        if (add)
        {
            // Find season 1, episode 1 with its video file
            Episode? season1Episode1 = await context.Episodes
                .Include(e => e.VideoFiles)
                .FirstOrDefaultAsync(e => e.TvId == tvId && e.SeasonNumber == 1 && e.EpisodeNumber == 1);
    
            if (season1Episode1 is not null && season1Episode1.VideoFiles.Any())
            {
                VideoFile videoFile = season1Episode1.VideoFiles.First();
                
                // Check if userdata already exists for this video file
                UserData? existingUserData = await context.UserData
                    .FirstOrDefaultAsync(ud => ud.UserId == userId && ud.VideoFileId == videoFile.Id);
    
                if (existingUserData is null)
                {
                    context.UserData.Add(new()
                    {
                        UserId = userId,
                        VideoFileId = videoFile.Id,
                        TvId = tvId,
                        Time = 0,
                        LastPlayedDate = DateTime.UtcNow.ToString("o"),
                        Type = "tv"
                    });
                }
            }
        }
        else
        {
            // Remove all userdata for this tv show
            List<UserData> userDataToRemove = await context.UserData
                .Where(ud => ud.UserId == userId && ud.TvId == tvId)
                .ToListAsync();
    
            context.UserData.RemoveRange(userDataToRemove);
        }
    
        await context.SaveChangesAsync();
        return true;
    }
}
