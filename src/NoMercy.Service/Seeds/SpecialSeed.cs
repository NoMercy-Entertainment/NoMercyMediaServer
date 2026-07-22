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
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using NoMercy.Service.Seeds.Data;
using Serilog.Events;
using SpecialItem = NoMercy.Database.Models.TvShows.SpecialItem;

namespace NoMercy.Service.Seeds;

public static class SpecialSeed
{
    public static async Task Init(MediaContext context)
    {
        Logger.Setup(message: "Adding Special");

        try
        {
            Library movieLibrary = await context
                .Libraries.Where(predicate: f => f.Type == MediaTypes.MovieMediaType)
                .Include(navigationPropertyPath: l => l.FolderLibraries)
                    .ThenInclude(navigationPropertyPath: fl => fl.Folder)
                .FirstAsync();

            Library tvLibrary = await context
                .Libraries.Where(predicate: f => f.Type == MediaTypes.TvMediaType)
                .Include(navigationPropertyPath: l => l.FolderLibraries)
                    .ThenInclude(navigationPropertyPath: fl => fl.Folder)
                .FirstAsync();

            Special special = new()
            {
                Id = McuSeedData.Special.Id,
                Title = McuSeedData.Special.Title,
                Backdrop = McuSeedData.Special.Backdrop,
                Poster = McuSeedData.Special.Poster,
                Logo = McuSeedData.Special.Logo,
                Overview = McuSeedData.Special.Overview,
                Creator = McuSeedData.Special.Creator,
                _colorPalette = await NoMercyImageManager.MultiColorPalette(items:
                [
                    new(key: "poster", path: McuSeedData.Special.Poster),
                    new(key: "backdrop", path: McuSeedData.Special.Backdrop),
                ]),
            };

            await context
                .Specials.Upsert(entity: special)
                .On(match: v => new { v.Id })
                .WhenMatched(
                    updater: (si, su) =>
                        new()
                        {
                            Id = su.Id,
                            Title = su.Title,
                            Backdrop = su.Backdrop,
                            Poster = su.Poster,
                            Logo = su.Logo,
                            Overview = su.Overview,
                            Creator = su.Creator,
                            _colorPalette = su._colorPalette,
                        }
                )
                .RunAsync();

            TmdbSearchClient client = new();
            List<int> tvIds = [];
            List<int> movieIds = [];
            List<SpecialItem> specialItems = [];

            foreach (Dto.SpecialItem item in McuSeedData.McuItems)
            {
                Logger.Setup(message: $"Searching for {item.Title} ({item.Year})");
                switch (item.Type)
                {
                    case MediaTypes.MovieMediaType:
                        await AddMovieItem(
                            context: context,
                            client: client,
                            movieLibrary: movieLibrary,
                            item: item,
                            movieIds: movieIds,
                            specialItems: specialItems
                        );
                        break;
                    case MediaTypes.TvMediaType:
                    case MediaTypes.AnimeMediaType:
                        await AddTvItem(context: context, client: client, tvLibrary: tvLibrary, item: item, tvIds: tvIds, specialItems: specialItems);
                        break;
                }
            }

            await UpsertSpecialItems(context: context, specialItems: specialItems);
        }
        catch (Exception e)
        {
            Logger.Setup(message: e.Message, level: LogEventLevel.Fatal);
            throw;
        }
    }

    private static async Task AddMovieItem(
        MediaContext context,
        TmdbSearchClient client,
        Library movieLibrary,
        Dto.SpecialItem item,
        List<int> movieIds,
        List<SpecialItem> specialItems
    )
    {
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(
            query: item.Title,
            year: item.Year.ToString()
        );
        TmdbMovie? movie = result?.Results.FirstOrDefault(predicate: r =>
            !r.Title.ToLower().Contains(value: "making of")
        );

        if (movie is null || movieIds.Contains(item: movie.Id))
            return;

        movieIds.Add(item: movie.Id);

        try
        {
            bool exists = context.Movies.Any(predicate: x => x.Id == movie.Id);
            if (!exists)
            {
                MovieImportJob j = new() { Id = movie.Id, LibraryId = movieLibrary.Id };
                await j.Handle();
            }
        }
        catch (Exception e)
        {
            Logger.Setup(message: e.Message, level: LogEventLevel.Fatal);
        }

        specialItems.Add(
            item: new()
            {
                SpecialId = McuSeedData.Special.Id,
                MovieId = movie.Id,
                Order = specialItems.Count,
            }
        );
    }

    private static async Task AddTvItem(
        MediaContext context,
        TmdbSearchClient client,
        Library tvLibrary,
        Dto.SpecialItem item,
        List<int> tvIds,
        List<SpecialItem> specialItems
    )
    {
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(
            query: item.Title,
            year: item.Year.ToString()
        );
        TmdbTvShow? tv = result?.Results.FirstOrDefault(predicate: r =>
            !r.Name.Contains(value: "making of", comparisonType: StringComparison.InvariantCultureIgnoreCase)
        );

        if (tv is null || tvIds.Contains(item: tv.Id))
            return;

        tvIds.Add(item: tv.Id);

        try
        {
            bool exists = await context.Tvs.AnyAsync(predicate: x => x.Id == tv.Id);
            if (!exists)
            {
                ShowImportJob j = new() { Id = tv.Id, LibraryId = tvLibrary.Id };
                await j.Handle();
            }
        }
        catch (Exception e)
        {
            Logger.Setup(message: e.Message, level: LogEventLevel.Fatal);
        }

        if (item.Episodes.Length == 0)
            item.Episodes = await context
                .Episodes.Where(predicate: x => x.TvId == tv.Id)
                .Where(predicate: x => x.SeasonNumber == item.Seasons.First())
                .Select(selector: x => x.EpisodeNumber)
                .ToArrayAsync();

        foreach (int episodeNumber in item.Episodes)
        {
            Episode? episode = await context.Episodes.FirstOrDefaultAsync(predicate: x =>
                x.TvId == tv.Id
                && x.SeasonNumber == item.Seasons.First()
                && x.EpisodeNumber == episodeNumber
            );

            if (episode is null)
                continue;

            specialItems.Add(
                item: new()
                {
                    SpecialId = McuSeedData.Special.Id,
                    EpisodeId = episode.Id,
                    Order = specialItems.Count,
                }
            );
        }
    }

    private static async Task UpsertSpecialItems(
        MediaContext context,
        List<SpecialItem> specialItems
    )
    {
        Logger.Setup(message: $"Upsetting {specialItems.Count} SpecialItems");

        IEnumerable<SpecialItem> movies = specialItems.Where(predicate: s => s.MovieId is not null);

        foreach (SpecialItem movie in movies)
            try
            {
                await context
                    .SpecialItems.Upsert(entity: movie)
                    .On(match: x => new { x.SpecialId, x.MovieId })
                    .WhenMatched(
                        updater: (old, @new) =>
                            new()
                            {
                                SpecialId = @new.SpecialId,
                                MovieId = @new.MovieId,
                                Order = @new.Order,
                            }
                    )
                    .RunAsync();
            }
            catch (Exception e)
            {
                Logger.Error(message: e);
            }

        IEnumerable<SpecialItem> episodes = specialItems.Where(predicate: s => s.EpisodeId is not null);

        foreach (SpecialItem episode in episodes)
            try
            {
                await context
                    .SpecialItems.Upsert(entity: episode)
                    .On(match: x => new { x.SpecialId, x.EpisodeId })
                    .WhenMatched(
                        updater: (old, @new) =>
                            new()
                            {
                                SpecialId = @new.SpecialId,
                                EpisodeId = @new.EpisodeId,
                                Order = @new.Order,
                            }
                    )
                    .RunAsync();
            }
            catch (Exception e)
            {
                Logger.Error(message: e);
            }

        Logger.Setup(message: "SpecialItems Upset complete");
    }
}
