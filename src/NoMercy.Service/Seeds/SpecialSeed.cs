using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Images;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem.Information;
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
        Logger.Setup("Adding Special");

        try
        {
            Library movieLibrary = await context.Libraries
                .Where(f => f.Type == Config.MovieMediaType)
                .Include(l => l.FolderLibraries)
                .ThenInclude(fl => fl.Folder)
                .FirstAsync();

            Library tvLibrary = await context.Libraries
                .Where(f => f.Type == Config.TvMediaType)
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
                _colorPalette = await NoMercyImageManager
                    .MultiColorPalette([
                        new("poster", McuSeedData.Special.Poster),
                        new("backdrop", McuSeedData.Special.Backdrop)
                    ])
            };

            await context.Specials
                .Upsert(special)
                .On(v => new { v.Id })
                .WhenMatched((si, su) => new()
                {
                    Id = su.Id,
                    Title = su.Title,
                    Backdrop = su.Backdrop,
                    Poster = su.Poster,
                    Logo = su.Logo,
                    Overview = su.Overview,
                    Creator = su.Creator,
                    _colorPalette = su._colorPalette
                })
                .RunAsync();

            TmdbSearchClient client = new();
            List<int> tvIds = [];
            List<int> movieIds = [];
            List<SpecialItem> specialItems = [];

            foreach (Dto.SpecialItem item in McuSeedData.McuItems)
            {
                Logger.Setup($"Searching for {item.Title} ({item.Year})");
                switch (item.Type)
                {
                    case Config.MovieMediaType:
                        await AddMovieItem(context, client, movieLibrary, item, movieIds, specialItems);
                        break;
                    case Config.TvMediaType:
                        await AddTvItem(context, client, tvLibrary, item, tvIds, specialItems);
                        break;
                }
            }

            await UpsertSpecialItems(context, specialItems);
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
            throw;
        }
    }

    private static async Task AddMovieItem(MediaContext context, TmdbSearchClient client, Library movieLibrary,
        Dto.SpecialItem item, List<int> movieIds, List<SpecialItem> specialItems)
    {
        TmdbPaginatedResponse<TmdbMovie>? result = await client.Movie(item.Title, item.Year.ToString());
        TmdbMovie? movie = result?.Results.FirstOrDefault(r => !r.Title.ToLower().Contains("making of"));

        if (movie is null || movieIds.Contains(movie.Id)) return;

        movieIds.Add(movie.Id);

        try
        {
            bool exists = context.Movies.Any(x => x.Id == movie.Id);
            if (!exists)
            {
                MovieImportJob j = new() { Id = movie.Id, LibraryId = movieLibrary.Id };
                await j.Handle();
            }
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }

        specialItems.Add(new()
        {
            SpecialId = McuSeedData.Special.Id,
            MovieId = movie.Id,
            Order = specialItems.Count
        });
    }

    private static async Task AddTvItem(MediaContext context, TmdbSearchClient client, Library tvLibrary,
        Dto.SpecialItem item, List<int> tvIds, List<SpecialItem> specialItems)
    {
        TmdbPaginatedResponse<TmdbTvShow>? result = await client.TvShow(item.Title, item.Year.ToString());
        TmdbTvShow? tv = result?.Results.FirstOrDefault(r =>
            !r.Name.Contains("making of", StringComparison.InvariantCultureIgnoreCase));

        if (tv is null || tvIds.Contains(tv.Id)) return;

        tvIds.Add(tv.Id);

        try
        {
            bool exists = context.Tvs.Any(x => x.Id == tv.Id);
            if (!exists)
            {
                ShowImportJob j = new() { Id = tv.Id, LibraryId = tvLibrary.Id };
                await j.Handle();
            }
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }

        if (item.Episodes.Length == 0)
            item.Episodes = context.Episodes
                .Where(x => x.TvId == tv.Id)
                .Where(x => x.SeasonNumber == item.Seasons.First())
                .Select(x => x.EpisodeNumber)
                .ToArray();

        foreach (int episodeNumber in item.Episodes)
        {
            Episode? episode = context.Episodes
                .FirstOrDefault(x =>
                    x.TvId == tv.Id
                    && x.SeasonNumber == item.Seasons.First()
                    && x.EpisodeNumber == episodeNumber);

            if (episode is null) continue;

            specialItems.Add(new()
            {
                SpecialId = McuSeedData.Special.Id,
                EpisodeId = episode.Id,
                Order = specialItems.Count
            });
        }
    }

    private static async Task UpsertSpecialItems(MediaContext context, List<SpecialItem> specialItems)
    {
        Logger.Setup($"Upsetting {specialItems.Count} SpecialItems");

        IEnumerable<SpecialItem> movies = specialItems
            .Where(s => s.MovieId is not null);

        foreach (SpecialItem movie in movies)
            try
            {
                await context.SpecialItems.Upsert(movie)
                    .On(x => new { x.SpecialId, x.MovieId })
                    .WhenMatched((old, @new) => new()
                    {
                        SpecialId = @new.SpecialId,
                        MovieId = @new.MovieId,
                        Order = @new.Order
                    })
                    .RunAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        IEnumerable<SpecialItem> episodes = specialItems
            .Where(s => s.EpisodeId is not null);

        foreach (SpecialItem episode in episodes)
            try
            {
                await context.SpecialItems.Upsert(episode)
                    .On(x => new { x.SpecialId, x.EpisodeId })
                    .WhenMatched((old, @new) => new()
                    {
                        SpecialId = @new.SpecialId,
                        EpisodeId = @new.EpisodeId,
                        Order = @new.Order
                    })
                    .RunAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        Logger.Setup("SpecialItems Upset complete");
    }
}