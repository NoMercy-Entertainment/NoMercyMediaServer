// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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
using NoMercy.Providers.KitsuIo;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.Data.Repositories;

public class TvShowRepository(IDbContextFactory<MediaContext> contextFactory) : ITvShowRepository
{
    public async Task<TvDetail?> GetTvAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        // Query 1: Core TV data — show metadata, seasons/episodes, show-level cast/crew, etc.
        // Removed: AlternativeTitles (unused by DTO), Library.LibraryUsers (only needed in WHERE)
        // Episode cast/crew split to Query 2 to reduce round-trips
        Tv? tv = await context
            .Tvs.AsNoTracking()
            .Where(predicate: tv => tv.Id == id)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: tv => tv.TvUser)
            .Include(navigationPropertyPath: tv => tv.Media.Where(media => media.Type == "Trailer"))
            .Include(navigationPropertyPath: tv => tv.Translations.Where(translation => translation.Iso6391 == language))
            .Include(navigationPropertyPath: tv =>
                tv.Images.Where(image =>
                        (image.Type == "logo" && image.Iso6391 == "en")
                        || (
                            (image.Type == "backdrop" || image.Type == "poster")
                            && (image.Iso6391 == "en" || image.Iso6391 == null)
                        )
                    )
                    .OrderByDescending(image => image.VoteAverage)
                    .ThenBy(image => image.Id)
            )
            .Include(navigationPropertyPath: tv =>
                tv.CertificationTvs.Where(certification =>
                    certification.Certification.Iso31661 == country
                    || certification.Certification.Iso31661 == "US"
                )
            )
                .ThenInclude(navigationPropertyPath: certificationTv => certificationTv.Certification)
            .Include(navigationPropertyPath: tv => tv.Creators)
                .ThenInclude(navigationPropertyPath: genreTv => genreTv.Person)
            .Include(navigationPropertyPath: tv => tv.GenreTvs)
                .ThenInclude(navigationPropertyPath: genreTv => genreTv.Genre)
            .Include(navigationPropertyPath: tv => tv.KeywordTvs)
                .ThenInclude(navigationPropertyPath: keywordTv => keywordTv.Keyword)
            .Include(navigationPropertyPath: tv => tv.Cast)
                .ThenInclude(navigationPropertyPath: castTv => castTv.Person)
            .Include(navigationPropertyPath: tv => tv.Cast)
                .ThenInclude(navigationPropertyPath: castTv => castTv.Role)
            .Include(navigationPropertyPath: tv => tv.Crew)
                .ThenInclude(navigationPropertyPath: crewTv => crewTv.Person)
            .Include(navigationPropertyPath: tv => tv.Crew)
                .ThenInclude(navigationPropertyPath: crewTv => crewTv.Job)
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season =>
                    season.Translations.Where(translation => translation.Iso6391 == language)
                )
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season => season.Episodes)
                    .ThenInclude(navigationPropertyPath: episode =>
                        episode.Translations.Where(translation => translation.Iso6391 == language)
                    )
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season => season.Episodes)
                    .ThenInclude(navigationPropertyPath: episode => episode.VideoFiles)
                        .ThenInclude(navigationPropertyPath: file =>
                            file.UserData.Where(userData => userData.UserId.Equals(userId))
                        )
            .Include(navigationPropertyPath: tv => tv.Episodes)
                .ThenInclude(navigationPropertyPath: episode => episode.VideoFiles)
                    .ThenInclude(navigationPropertyPath: file =>
                        file.UserData.Where(userData => userData.UserId.Equals(userId))
                    )
            .Include(navigationPropertyPath: tv => tv.RecommendationFrom)
            .Include(navigationPropertyPath: tv => tv.SimilarFrom)
            .Include(navigationPropertyPath: tv => tv.WatchProviderMedia.Where(wpm => wpm.CountryCode == country))
                .ThenInclude(navigationPropertyPath: wpm => wpm.WatchProvider)
            .Include(navigationPropertyPath: tv => tv.NetworkTvs)
                .ThenInclude(navigationPropertyPath: ntv => ntv.Network)
            .Include(navigationPropertyPath: tv => tv.CompaniesTvs)
                .ThenInclude(navigationPropertyPath: ctv => ctv.Company)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken: ct);

        if (tv is null)
            return null;

        // Query 2: Episode-level cast/crew — loaded separately to reduce query complexity
        // This avoids 4 additional split-query round-trips in the main query
        List<Episode> episodesWithCastCrew = await context
            .Episodes.AsNoTracking()
            .Where(predicate: e => e.TvId == id)
            .Include(navigationPropertyPath: e => e.Cast)
                .ThenInclude(navigationPropertyPath: c => c.Person)
            .Include(navigationPropertyPath: e => e.Cast)
                .ThenInclude(navigationPropertyPath: c => c.Role)
            .Include(navigationPropertyPath: e => e.Crew)
                .ThenInclude(navigationPropertyPath: c => c.Person)
            .Include(navigationPropertyPath: e => e.Crew)
                .ThenInclude(navigationPropertyPath: c => c.Job)
            .AsSplitQuery()
            .ToListAsync(cancellationToken: ct);

        // Merge episode cast/crew into the main query results
        Dictionary<int, Episode> episodeLookup = episodesWithCastCrew.ToDictionary(keySelector: e => e.Id);
        foreach (Episode episode in tv.Episodes)
        {
            if (episodeLookup.TryGetValue(key: episode.Id, value: out Episode? loaded))
            {
                episode.Cast = loaded.Cast;
                episode.Crew = loaded.Crew;
            }
        }

        foreach (Season season in tv.Seasons)
        {
            foreach (Episode episode in season.Episodes)
            {
                if (episodeLookup.TryGetValue(key: episode.Id, value: out Episode? loaded))
                {
                    episode.Cast = loaded.Cast;
                    episode.Crew = loaded.Crew;
                }
            }
        }

        // Related shows (similar / recommended) enriched with availability, so the
        // DTO can render related cards without itself touching a DbContext.
        int[] similarIds = tv.SimilarFrom.Select(selector: similar => similar.MediaId).ToArray();
        Tv[] similars = await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => similarIds.Contains(t.Id))
            .Include(navigationPropertyPath: t => t.Episodes)
                .ThenInclude(navigationPropertyPath: episode => episode.VideoFiles)
            .ToArrayAsync(cancellationToken: ct);

        int[] recommendationIds = tv
            .RecommendationFrom.Select(selector: recommendation => recommendation.MediaId)
            .ToArray();
        Tv[] recommendations = await context
            .Tvs.AsNoTracking()
            .Where(predicate: t => recommendationIds.Contains(t.Id))
            .Include(navigationPropertyPath: t => t.Episodes)
                .ThenInclude(navigationPropertyPath: episode => episode.VideoFiles)
            .ToArrayAsync(cancellationToken: ct);

        return new(Tv: tv, Similars: similars, Recommendations: recommendations);
    }

    public async Task<Tv?> GetTvWithLibraryAsync(int id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Tvs.AsNoTracking()
            .Include(navigationPropertyPath: tv => tv.Library)
                .ThenInclude(navigationPropertyPath: library => library.FolderLibraries)
                    .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .FirstOrDefaultAsync(predicate: tv => tv.Id == id, cancellationToken: ct);
    }

    public async Task<bool> GetTvAvailableAsync(Guid userId, int id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Tvs.AsNoTracking()
            .ForUser(userId: userId)
            .Where(predicate: tv => tv.Id == id)
            .AnyAsync(
                predicate: tv =>
                    tv.Episodes.Any(e =>
                        (
                            e.VideoFiles.Any(v => v.Folder != null)
                            || e.Tv.Episodes.Any(o =>
                                o.SeasonNumber == e.SeasonNumber
                                && o.VideoFiles.Any(w =>
                                    w.Folder != null
                                    && w.LastEpisodeNumber != null
                                    && o.EpisodeNumber <= e.EpisodeNumber
                                    && e.EpisodeNumber <= (w.LastEpisodeNumber ?? 0)
                                )
                            )
                        )
                    ),
                cancellationToken: ct
            );
    }

    public async Task<Tv?> GetPlaylistAsync(
        Guid userId,
        int id,
        string language,
        string country,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        return await context
            .Tvs.AsNoTracking()
            .Where(predicate: tv => tv.Id == id)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: tv =>
                tv.Seasons.OrderBy(season => season.SeasonNumber).ThenBy(season => season.Id)
            )
                .ThenInclude(navigationPropertyPath: season =>
                    season
                        .Episodes.OrderBy(episode => episode.EpisodeNumber)
                        .ThenBy(episode => episode.Id)
                )
            .Include(navigationPropertyPath: tv => tv.Translations.Where(translation => translation.Iso6391 == language))
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season => season.Episodes)
                    .ThenInclude(navigationPropertyPath: tv => tv.Tv)
                        .ThenInclude(navigationPropertyPath: tv =>
                            tv.Translations.Where(translation => translation.Iso6391 == language)
                        )
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season => season.Episodes)
                    .ThenInclude(navigationPropertyPath: tv => tv.Tv)
                        .ThenInclude(navigationPropertyPath: tv => tv.Media.Where(media => media.Type == "video"))
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season => season.Episodes)
                    .ThenInclude(navigationPropertyPath: tv => tv.Tv)
                        .ThenInclude(navigationPropertyPath: tv =>
                            tv.Images.Where(image =>
                                image.Type == "logo"
                                && image.Iso6391 == "en"
                                && image.Width > image.Height
                            )
                        )
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season => season.Episodes)
                    .ThenInclude(navigationPropertyPath: tv => tv.VideoFiles)
                        .ThenInclude(navigationPropertyPath: videoFile => videoFile.Metadata)
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season => season.Episodes)
                    .ThenInclude(navigationPropertyPath: tv => tv.VideoFiles)
                        .ThenInclude(navigationPropertyPath: file =>
                            file.UserData.Where(userData =>
                                userData.UserId.Equals(userId) && userData.Type == "tv"
                            )
                        )
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season =>
                    season.Translations.Where(translation => translation.Iso6391 == language)
                )
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season => season.Episodes)
                    .ThenInclude(navigationPropertyPath: episode =>
                        episode.Translations.Where(translation => translation.Iso6391 == language)
                    )
            .Include(navigationPropertyPath: tv => tv.Seasons)
                .ThenInclude(navigationPropertyPath: season => season.Episodes)
                    .ThenInclude(navigationPropertyPath: tv => tv.Tv)
                        .ThenInclude(navigationPropertyPath: tv =>
                            tv.CertificationTvs.Where(certification =>
                                certification.Certification.Iso31661 == country
                                || certification.Certification.Iso31661 == "US"
                            )
                        )
                            .ThenInclude(navigationPropertyPath: certificationTv => certificationTv.Certification)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<bool> LikeAsync(
        int id,
        Guid userId,
        bool like,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        TvUser? tvUser = await context.TvUser.FirstOrDefaultAsync(
            predicate: tu => tu.TvId == id && tu.UserId == userId,
            cancellationToken: ct
        );

        if (like)
        {
            await context
                .TvUser.Upsert(entity: new(tvId: id, userId: userId))
                .On(match: m => new { m.TvId, m.UserId })
                .WhenMatched(updater: m => new() { TvId = m.TvId, UserId = m.UserId })
                .RunAsync();
        }
        else if (tvUser != null)
        {
            context.TvUser.Remove(entity: tvUser);
            await context.SaveChangesAsync(cancellationToken: ct);
        }

        return true;
    }

    public async Task AddTvShowAsync(int id)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();
        TmdbTvClient tvClient = new(id: id);
        TmdbTvShowDetails? show = await tvClient.Details(priority: true);
        if (show == null)
            return;

        bool isAnime = await KitsuIoClient.IsAnime(title: show.Name, year: show.FirstAirDate.ParseYear());

        // Require Japanese origin to avoid false positives on western co-productions
        if (
            isAnime
            && !show.OriginCountry.Any(predicate: c =>
                string.Equals(a: c, b: "JP", comparisonType: StringComparison.OrdinalIgnoreCase)
            )
        )
            isAnime = false;

        Library? tvLibrary =
            await context
                .Libraries.Where(predicate: f => f.Type == (isAnime ? "anime" : "tv"))
                .FirstOrDefaultAsync()
            ?? await context.Libraries.Where(predicate: f => f.Type == "tv").FirstOrDefaultAsync();

        if (tvLibrary == null)
            return;

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<ShowImportJob>(id: id, library: tvLibrary);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        // SQLite schema uses DeleteBehavior.Restrict globally — the modelBuilder
        // cascade rules only affect EF's tracked-entity cascades, not bulk
        // ExecuteDeleteAsync. Without disabling FK enforcement the delete
        // throws on the first dependent row (seasons, episodes, video files,
        // userdata, etc.) and the controller returns 500. Mirrors the same
        // workaround applied in MovieRepository / CollectionRepository.
        //
        // PRAGMA foreign_keys is per-connection in SQLite. EF Core's
        // ExecuteSqlRawAsync and ExecuteDeleteAsync each open and close a pooled
        // connection by default — PRAGMA OFF on connection A doesn't apply to
        // the DELETE on connection B. Pin one connection across all three calls.
        bool ownsConnection =
            context.Database.GetDbConnection().State != System.Data.ConnectionState.Open;

        if (ownsConnection)
            await context.Database.OpenConnectionAsync(cancellationToken: ct);

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = OFF", cancellationToken: ct);
            try
            {
                await context.Tvs.Where(predicate: tv => tv.Id == id).ExecuteDeleteAsync(cancellationToken: ct);
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = ON", cancellationToken: ct);
            }
        }
        finally
        {
            if (ownsConnection)
                await context.Database.CloseConnectionAsync();
        }
    }

    public async Task<IEnumerable<Episode>> GetMissingLibraryShows(
        Guid userId,
        int id,
        string language,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Tv? tv = await context
            .Tvs.AsNoTracking()
            .Where(predicate: tv => tv.Id == id)
            .ForUser(userId: userId)
            .Include(navigationPropertyPath: tv => tv.Episodes.Where(e => !e.VideoFiles.Any()))
                .ThenInclude(navigationPropertyPath: e => e.Translations.Where(t => t.Iso6391 == language))
            .FirstOrDefaultAsync(cancellationToken: ct);

        return tv?.Episodes ?? [];
    }

    public async Task<bool> AddToWatchListAsync(
        int tvId,
        Guid userId,
        bool add = true,
        CancellationToken ct = default
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync(cancellationToken: ct);
        Tv? tv = await context.Tvs.AsNoTracking().FirstOrDefaultAsync(predicate: t => t.Id == tvId, cancellationToken: ct);

        if (tv is null)
            return false;

        if (add)
        {
            // Find season 1, episode 1 with its video file
            Episode? season1Episode1 = await context
                .Episodes.Include(navigationPropertyPath: e => e.VideoFiles)
                .FirstOrDefaultAsync(
                    predicate: e => e.TvId == tvId && e.SeasonNumber == 1 && e.EpisodeNumber == 1,
                    cancellationToken: ct
                );

            if (season1Episode1 is not null && season1Episode1.VideoFiles.Any())
            {
                VideoFile videoFile = season1Episode1.VideoFiles.First();

                // Check if userdata already exists for this video file
                UserData? existingUserData = await context.UserData.FirstOrDefaultAsync(
                    predicate: ud => ud.UserId == userId && ud.VideoFileId == videoFile.Id,
                    cancellationToken: ct
                );

                if (existingUserData is null)
                {
                    context.UserData.Add(
                        entity: new()
                        {
                            UserId = userId,
                            VideoFileId = videoFile.Id,
                            TvId = tvId,
                            Time = 0,
                            LastPlayedDate = DateTime.UtcNow.ToString(format: "o"),
                            Type = "tv",
                        }
                    );
                }
            }
        }
        else
        {
            // Remove all userdata for this tv show
            List<UserData> userDataToRemove = await context
                .UserData.Where(predicate: ud => ud.UserId == userId && ud.TvId == tvId)
                .ToListAsync(cancellationToken: ct);

            context.UserData.RemoveRange(entities: userDataToRemove);
        }

        await context.SaveChangesAsync(cancellationToken: ct);
        return true;
    }
}
