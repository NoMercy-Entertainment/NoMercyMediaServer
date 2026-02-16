using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;

namespace NoMercy.MediaProcessing.EventHandlers;

public class FileWatcherEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly SemaphoreSlim _semaphore = new(2);

    public FileWatcherEventHandler(IEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<FileCreatedEvent>(OnFileCreated));
        _subscriptions.Add(eventBus.Subscribe<FileDeletedEvent>(OnFileDeleted));
        _subscriptions.Add(eventBus.Subscribe<FileRenamedEvent>(OnFileRenamed));
    }

    internal async Task OnFileCreated(FileCreatedEvent @event, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            Logger.System($"FileWatcher: Processing new/changed content in {@event.FolderPath}", LogEventLevel.Information);

            MediaScan mediaScan = new();
            MediaScan scan = mediaScan.EnableFileListing();

            if (@event.LibraryType == Config.MusicMediaType)
                scan.DisableRegexFilter();

            ConcurrentBag<MediaFolderExtend> mediaFolders = await scan.Process(@event.FolderPath);

            if (mediaFolders.Count == 0)
            {
                Logger.System($"FileWatcher: No media found in {@event.FolderPath}", LogEventLevel.Warning);
                return;
            }

            MediaFolderExtend mediaFolder = mediaFolders.First();

            switch (@event.LibraryType)
            {
                case Config.MovieMediaType:
                    await HandleMovieFolder(@event, mediaFolder);
                    break;
                case Config.TvMediaType:
                    await HandleTvFolder(@event, mediaFolder);
                    break;
                case Config.MusicMediaType:
                    HandleMusicFolder(@event, mediaFolder);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.System($"FileWatcher: Error processing {@event.FolderPath}: {ex.Message}", LogEventLevel.Error);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    internal async Task OnFileDeleted(FileDeletedEvent @event, CancellationToken ct)
    {
        try
        {
            Logger.System($"FileWatcher: Processing deletion of {@event.FullPath}", LogEventLevel.Information);

            string hostFolder = Path.GetDirectoryName(@event.FullPath) ?? string.Empty;
            string filename = "/" + Path.GetFileName(@event.FullPath);

            await using MediaContext mediaContext = new();
            FileRepository fileRepository = new(mediaContext);

            int videoFilesDeleted = await fileRepository.DeleteVideoFilesByHostFolderAsync(hostFolder);
            int metadataDeleted = await fileRepository.DeleteMetadataByHostFolderAsync(hostFolder);

            Logger.System($"FileWatcher: Deleted {videoFilesDeleted} video file(s) and {metadataDeleted} metadata record(s) for {hostFolder}", LogEventLevel.Information);

            if (videoFilesDeleted > 0 && EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
                {
                    QueryKey = ["base", "libraries"]
                });
            }
        }
        catch (Exception ex)
        {
            Logger.System($"FileWatcher: Error processing deletion of {@event.FullPath}: {ex.Message}", LogEventLevel.Error);
        }
    }

    internal async Task OnFileRenamed(FileRenamedEvent @event, CancellationToken ct)
    {
        try
        {
            Logger.System($"FileWatcher: Processing rename from {@event.OldFullPath} to {@event.NewFullPath}", LogEventLevel.Information);

            string oldHostFolder = Path.GetDirectoryName(@event.OldFullPath) ?? string.Empty;
            string oldFilename = "/" + Path.GetFileName(@event.OldFullPath);
            string newHostFolder = Path.GetDirectoryName(@event.NewFullPath) ?? string.Empty;
            string newFilename = "/" + Path.GetFileName(@event.NewFullPath);

            await using MediaContext mediaContext = new();
            FileRepository fileRepository = new(mediaContext);

            int updated = await fileRepository.UpdateVideoFilePathsAsync(oldHostFolder, oldFilename, newHostFolder, newFilename);

            if (updated > 0)
            {
                Logger.System($"FileWatcher: Updated {updated} video file path(s) from {oldHostFolder} to {newHostFolder}", LogEventLevel.Information);

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(new LibraryRefreshEvent
                    {
                        QueryKey = ["base", "libraries"]
                    });
                }
            }
            else
            {
                Logger.System($"FileWatcher: No matching records found for rename, treating as new content", LogEventLevel.Debug);
                await OnFileCreated(new FileCreatedEvent
                {
                    FolderPath = newHostFolder,
                    LibraryId = @event.LibraryId,
                    LibraryType = @event.LibraryType
                }, ct);
            }
        }
        catch (Exception ex)
        {
            Logger.System($"FileWatcher: Error processing rename from {@event.OldFullPath} to {@event.NewFullPath}: {ex.Message}", LogEventLevel.Error);
        }
    }

    private static async Task HandleMovieFolder(FileCreatedEvent @event, MediaFolderExtend mediaFolder)
    {
        if (mediaFolder.Parsed?.Title is null)
        {
            Logger.System($"FileWatcher: Could not parse title from {mediaFolder.Path}", LogEventLevel.Warning);
            return;
        }

        Logger.System($"FileWatcher: Movie {mediaFolder.Path}: Searching TMDB for '{mediaFolder.Parsed.Title}'", LogEventLevel.Information);

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbMovie>? response =
            await tmdbSearchClient.Movie(mediaFolder.Parsed.Title, mediaFolder.Parsed.Year);

        if (response?.Results is null || response.Results.Count == 0)
        {
            Logger.System($"FileWatcher: No TMDB results found for movie '{mediaFolder.Parsed.Title}'", LogEventLevel.Warning);
            return;
        }

        TmdbMovie movie = response.Results.First();
        Logger.System($"FileWatcher: Movie '{movie.Title}' found on TMDB (ID: {movie.Id}), dispatching job", LogEventLevel.Information);

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AddMovieJob>(movie.Id, @event.LibraryId);
    }

    private static async Task HandleTvFolder(FileCreatedEvent @event, MediaFolderExtend mediaFolder)
    {
        if (mediaFolder.Parsed?.Title is null)
        {
            Logger.System($"FileWatcher: Could not parse title from {mediaFolder.Path}", LogEventLevel.Warning);
            return;
        }

        Logger.System($"FileWatcher: TV Show {mediaFolder.Path}: Searching TMDB for '{mediaFolder.Parsed.Title}'", LogEventLevel.Information);

        using TmdbSearchClient tmdbSearchClient = new();
        TmdbPaginatedResponse<TmdbTvShow>? response =
            await tmdbSearchClient.TvShow(mediaFolder.Parsed.Title, mediaFolder.Parsed.Year);

        if (response?.Results is null || response.Results.Count == 0)
        {
            Logger.System($"FileWatcher: No TMDB results found for TV show '{mediaFolder.Parsed.Title}'", LogEventLevel.Warning);
            return;
        }

        TmdbTvShow show = response.Results.First();
        Logger.System($"FileWatcher: TV Show '{show.Name}' found on TMDB (ID: {show.Id}), dispatching job", LogEventLevel.Information);

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AddShowJob>(show.Id, @event.LibraryId);
    }

    private static void HandleMusicFolder(FileCreatedEvent @event, MediaFolderExtend mediaFolder)
    {
        Logger.System($"FileWatcher: Music {mediaFolder.Path}: Processing", LogEventLevel.Information);

        string directoryPath = Path.GetFullPath(mediaFolder.Path);

        using MediaContext mediaContext = new();
        Library? library = mediaContext.Libraries
            .Include(l => l.FolderLibraries)
            .ThenInclude(fl => fl.Folder)
            .FirstOrDefault(l => l.Id == @event.LibraryId);

        if (library is null) return;

        FolderLibrary? folderLibrary =
            library.FolderLibraries.FirstOrDefault(f => directoryPath.Contains(f.Folder.Path));
        if (folderLibrary is null) return;

        JobDispatcher jobDispatcher = new();
        jobDispatcher.DispatchJob<AudioImportJob>(@event.LibraryId, folderLibrary.FolderId, directoryPath);
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }
}
