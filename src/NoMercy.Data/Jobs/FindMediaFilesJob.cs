using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Data.Logic;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Factory;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Data.Jobs;

[Serializable]
public class FindMediaFilesJob : IShouldQueue
{
    public string QueueName => "import";
    public int Priority => 5;

    public int Id { get; set; }
    public Library? Library { get; set; }

    public FindMediaFilesJob()
    {
        //
    }

    public FindMediaFilesJob(int id, Library library)
    {
        Id = id;
        Library = library;
    }

    public async Task Handle()
    {
        if (Library == null)
            return;

        Logger.Queue($"Finding media files for {Id} in library {Library.Id.ToString()}");

        await using MediaContext context = new();
        Library? library = await context
            .Libraries.AsTracking()
            .Where(f => f.Id == Library.Id)
            .Include(l => l.FolderLibraries)
                .ThenInclude(fl => fl.Folder)
            .Include(l => l.LibraryMovies.Where(lm => lm.Movie.Id == Id))
                .ThenInclude(lm => lm.Movie)
            .Include(l => l.LibraryTvs.Where(lt => lt.Tv.Id == Id))
                .ThenInclude(lt => lt.Tv)
            .FirstOrDefaultAsync();

        if (library == null)
            return;

        IStorageDriver storageDriver = new LocalStorageDriver();
        InlineDriverConfigResolver driverConfigResolver = new(context);
        IStorageFactory storageFactory = new StorageFactory(
            storageDriver,
            NullLogger<StorageFactory>.Instance,
            driverConfigResolver
        );
        await using FileLogic file = new(Id, library, context, storageFactory, storageDriver);
        await file.Process();

        if (file.Files.Count > 0)
        {
            Logger.App($"Found {file.Files.Count} files in {file.Files.FirstOrDefault()?.Path}");

            if (library.LibraryMovies.Count > 0)
            {
                LibraryMovie? libraryMovie = library.LibraryMovies.FirstOrDefault();
                if (libraryMovie == null)
                    return;

                libraryMovie.Movie.Folder = file.Files.FirstOrDefault()?.Path;

                await context.SaveChangesAsync();
            }
            else if (library.LibraryTvs.Count > 0)
            {
                LibraryTv? libraryTv = library.LibraryTvs.FirstOrDefault();
                if (libraryTv == null)
                    return;

                libraryTv.Tv.Folder = file.Files.FirstOrDefault()?.Path;

                await context.SaveChangesAsync();
            }

            if (EventBusProvider.IsConfigured)
                await EventBusProvider.Current.PublishAsync(
                    new LibraryRefreshEvent { QueryKey = ["libraries", library.Id.ToString()] }
                );
        }

        if (EventBusProvider.IsConfigured)
            await EventBusProvider.Current.PublishAsync(
                new LibraryRefreshEvent
                {
                    QueryKey =
                    [
                        library.Type == Config.MovieMediaType
                            ? Config.MovieMediaType
                            : Config.TvMediaType,
                        Id,
                    ],
                }
            );
    }

    /// <summary>
    /// Inline resolver that queries drivers from an already-open MediaContext.
    /// Used here to avoid requiring IDbContextFactory in a self-contained job.
    /// </summary>
    private sealed class InlineDriverConfigResolver(MediaContext context) : IDriverConfigResolver
    {
        public (string Type, string? ConfigJson)? Resolve(Ulid driverId)
        {
            NoMercy.Database.Models.Storage.Driver? driver = context
                .Drivers.AsNoTracking()
                .FirstOrDefault(d => d.Id == driverId);

            return driver is null ? null : (driver.Type, driver.Config);
        }
    }
}
