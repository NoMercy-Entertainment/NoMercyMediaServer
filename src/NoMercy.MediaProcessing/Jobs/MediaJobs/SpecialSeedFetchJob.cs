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
using NoMercy.Data.Data;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.Dto;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class SpecialSeedFetchJob : AbstractJob
{
    public override string QueueName => "import";
    public override int Priority => 10;

    public SpecialSeedFetchJob()
    {
        //
    }
    
    public override async Task Handle()
    {
        Logger.Setup("Adding Special");

        try
        {
            await using MediaContext mediaContext = new();
            JobDispatcher jobDispatcher = new();
            
            Library movieLibrary = await mediaContext
                .Libraries.Where(f => f.Type == MediaTypes.MovieMediaType)
                .Include(l => l.FolderLibraries)
                    .ThenInclude(fl => fl.Folder)
                .FirstAsync();

            Library tvLibrary = await mediaContext
                .Libraries.Where(f => f.Type == MediaTypes.TvMediaType)
                .Include(l => l.FolderLibraries)
                    .ThenInclude(fl => fl.Folder)
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
                _colorPalette = await NoMercyImageManager.MultiColorPalette([
                    new("poster", McuSeedData.Special.Poster),
                    new("backdrop", McuSeedData.Special.Backdrop),
                ]),
            };

            await mediaContext
                .Specials.Upsert(special)
                .On(v => new { v.Id })
                .WhenMatched((si, su) =>
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

            foreach (SpecialSeedItem item in McuSeedData.McuItems)
            {
                switch (item.Type)
                {
                    case MediaTypes.MovieMediaType:
                        await AddMovieItem(
                            mediaContext,
                            client,
                            movieLibrary,
                            item,
                            movieIds,
                            jobDispatcher
                        );
                        break;
                    case MediaTypes.TvMediaType:
                    case MediaTypes.AnimeMediaType:
                        await AddTvItem(
                            mediaContext, 
                            client, 
                            tvLibrary, 
                            item, 
                            tvIds, 
                            jobDispatcher
                        );
                        break;
                }
            }
        
            jobDispatcher.DispatchJob<SpecialSeedStoreJob>();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
            throw;
        }
    }

    private static async Task AddMovieItem(
        MediaContext context,
        TmdbSearchClient client,
        Library movieLibrary,
        SpecialSeedItem item,
        List<int> movieIds,
        JobDispatcher jobDispatcher
    )
    {
        Logger.Setup($"Searching for {item.Title} ({item.Year})");
        
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(
            item.Title,
            item.Year.ToString()
        );
        
        TmdbMovie? movie = result?.Results.FirstOrDefault(r =>
            !r.Title.ToLower().Contains("making of")
        );

        if (movie is null || movieIds.Contains(movie.Id))
            return;

        movieIds.Add(movie.Id);

        try
        {
            bool exists = context.Movies.Any(m => m.Id == movie.Id);
            if (!exists)
            {
                jobDispatcher.DispatchJob<MovieImportJob>(movie.Id, movieLibrary.Id);
            }
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }

    private static async Task AddTvItem(
        MediaContext context,
        TmdbSearchClient client,
        Library tvLibrary,
        SpecialSeedItem item,
        List<int> tvIds,
        JobDispatcher jobDispatcher
    )
    {
        Logger.Setup($"Searching for {item.Title} ({item.Year})");
        
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(
            item.Title,
            item.Year.ToString()
        );
        
        TmdbTvShow? tv = result?.Results.FirstOrDefault(r =>
            !r.Name.Contains("making of", StringComparison.InvariantCultureIgnoreCase)
        );

        if (tv is null || tvIds.Contains(tv.Id))
            return;

        tvIds.Add(tv.Id);

        try
        {
            bool exists = await context.Tvs.AnyAsync(x => x.Id == tv.Id);
            if (!exists)
            {
                jobDispatcher.DispatchJob<ShowImportJob>(tv.Id, tvLibrary.Id);
            }
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
