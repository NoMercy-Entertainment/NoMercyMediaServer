using Microsoft.EntityFrameworkCore;
using NoMercy.Data.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.Other;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Data.Repositories;

public class TvShowRepository(MediaContext context)
{
    
    public async Task<Tv?> GetTvAsync(MediaContext mediaContext, Guid userId, int id, string language, string country, CancellationToken ct = default)
    {
        // Query 1: Core TV data — show metadata, seasons/episodes, show-level cast/crew, etc.
        // Removed: AlternativeTitles (unused by DTO), Library.LibraryUsers (only needed in WHERE)
        // Episode cast/crew split to Query 2 to reduce round-trips
        Tv? tv = await mediaContext.Tvs.AsNoTracking()
            .Where(tv => tv.Id == id)
            .ForUser(userId)
            .Include(tv => tv.TvUser)
            .Include(tv => tv.Media
                .Where(media => media.Type == "Trailer"))
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
            .Include(tv => tv.WatchProviderMedia
                .Where(wpm => wpm.CountryCode == country))
                .ThenInclude(wpm => wpm.WatchProvider)
            .Include(tv => tv.NetworkTvs)
                .ThenInclude(ntv => ntv.Network)
            .Include(tv => tv.CompaniesTvs)
                .ThenInclude(ctv => ctv.Company)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        if (tv is null) return null;

        // Query 2: Episode-level cast/crew — loaded separately to reduce query complexity
        // This avoids 4 additional split-query round-trips in the main query
        List<Episode> episodesWithCastCrew = await mediaContext.Episodes.AsNoTracking()
            .Where(e => e.TvId == id)
            .Include(e => e.Cast)
                .ThenInclude(c => c.Person)
            .Include(e => e.Cast)
                .ThenInclude(c => c.Role)
            .Include(e => e.Crew)
                .ThenInclude(c => c.Person)
            .Include(e => e.Crew)
                .ThenInclude(c => c.Job)
            .AsSplitQuery()
            .ToListAsync(ct);

        // Merge episode cast/crew into the main query results
        Dictionary<int, Episode> episodeLookup = episodesWithCastCrew.ToDictionary(e => e.Id);
        foreach (Episode episode in tv.Episodes)
        {
            if (episodeLookup.TryGetValue(episode.Id, out Episode? loaded))
            {
                episode.Cast = loaded.Cast;
                episode.Crew = loaded.Crew;
            }
        }

        foreach (Season season in tv.Seasons)
        {
            foreach (Episode episode in season.Episodes)
            {
                if (episodeLookup.TryGetValue(episode.Id, out Episode? loaded))
                {
                    episode.Cast = loaded.Cast;
                    episode.Crew = loaded.Crew;
                }
            }
        }

        return tv;
    }

    public Task<bool> GetTvAvailableAsync(Guid userId, int id, CancellationToken ct = default)
    {
        return context.Tvs
            .AsNoTracking()
            .ForUser(userId)
            .Where(tv => tv.Id == id)
            .AnyAsync(tv => tv.Episodes.Any(e => e.VideoFiles.Any(v => v.Folder != null)), ct);
    }

    public async Task<Tv?> GetPlaylistAsync(Guid userId, int id, string language, string country, CancellationToken ct = default)
    {
        return await context.Tvs.AsNoTracking()
            .Where(tv => tv.Id == id)
            .ForUser(userId)
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
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> LikeAsync(int id, Guid userId, bool like, CancellationToken ct = default)
    {
        TvUser? tvUser = await context.TvUser
            .FirstOrDefaultAsync(tu => tu.TvId == id && tu.UserId == userId, ct);

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
            await context.SaveChangesAsync(ct);
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

    public Task DeleteAsync(int id, CancellationToken ct = default)
    {
        return context.Tvs
            .Where(tv => tv.Id == id)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<IEnumerable<Episode>> GetMissingLibraryShows(Guid userId, int id, string language, CancellationToken ct = default)
    {
        Tv? tv = await context.Tvs
            .AsNoTracking()
            .Where(tv => tv.Id == id)
            .ForUser(userId)
            .Include(tv => tv.Episodes.Where(e => !e.VideoFiles.Any()))
                .ThenInclude(e => e.Translations.Where(t => t.Iso6391 == language))
            .FirstOrDefaultAsync(ct);

        return tv?.Episodes ?? [];
    }
    
    public async Task<bool> AddToWatchListAsync(int tvId, Guid userId, bool add = true, CancellationToken ct = default)
    {
        Tv? tv = await context.Tvs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tvId, ct);

        if (tv is null)
            return false;

        if (add)
        {
            // Find season 1, episode 1 with its video file
            Episode? season1Episode1 = await context.Episodes
                .Include(e => e.VideoFiles)
                .FirstOrDefaultAsync(e => e.TvId == tvId && e.SeasonNumber == 1 && e.EpisodeNumber == 1, ct);

            if (season1Episode1 is not null && season1Episode1.VideoFiles.Any())
            {
                VideoFile videoFile = season1Episode1.VideoFiles.First();

                // Check if userdata already exists for this video file
                UserData? existingUserData = await context.UserData
                    .FirstOrDefaultAsync(ud => ud.UserId == userId && ud.VideoFileId == videoFile.Id, ct);

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
                .ToListAsync(ct);

            context.UserData.RemoveRange(userDataToRemove);
        }

        await context.SaveChangesAsync(ct);
        return true;
    }
}
