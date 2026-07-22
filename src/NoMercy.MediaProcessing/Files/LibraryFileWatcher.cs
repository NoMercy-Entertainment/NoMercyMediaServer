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

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Files;

public class LibraryFileWatcher
{
    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<LibraryFileWatcher> _instance = new(valueFactory: () =>
        new(storageDriver: _driverStore!, storageFactory: _storageFactoryStore!)
    );
    public static LibraryFileWatcher Instance => _instance.Value;

    private static IStorageDriver? _driverStore;
    private static IStorageFactory? _storageFactoryStore;

    private static FolderWatcher Fs => field ??= new(storageDriver: _driverStore!);
    private static IStorageDriver StorageDriver => _driverStore!;
    private static IStorageFactory StorageFactory => _storageFactoryStore!;

    private static readonly Dictionary<string, FileChangeGroup> FileChangeGroups = new();
    private static readonly Lock LockObject = new();

    private static readonly Regex EncodingOutputRegex = new(
        pattern: @"^(video_.*|audio_.*|subtitles|fonts|thumbs|metadata|scans|cds.*|NCED|NCOP)$",
        options: RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private const int Delay = 10;

    private static List<Library> _libraries = [];

    public LibraryFileWatcher(IStorageDriver storageDriver, IStorageFactory storageFactory)
    {
        _driverStore = storageDriver;
        _storageFactoryStore = storageFactory;
        Logger.System(message: "Starting FileSystem Watcher", level: LogEventLevel.Debug);

        Fs.OnChanged += _onFileChanged;
        Fs.OnCreated += _onFileCreated;
        Fs.OnDeleted += _onFileDeleted;
        Fs.OnRenamed += _onFileRenamed;
        Fs.OnError += _onError;

        RefreshLibraryCache();
        Parallel.ForEach(source: _libraries, body: library => AddLibraryWatcher(library: library));
    }

    public static void RefreshLibraryCache()
    {
        using MediaContext mediaContext = new();
        _libraries = mediaContext
            .Libraries.Include(navigationPropertyPath: library => library.FolderLibraries)
                .ThenInclude(navigationPropertyPath: folderLibrary => folderLibrary.Folder)
            .ToList();
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public static Action AddLibraryWatcher(Library library)
    {
        // Resolve through the driver, not the IStorage facade: the facade's
        // GetFullPath is a LocalStorage-only escape hatch that throws on every
        // remote backend. FolderWatcher.CreateWatcher supports network (UNC)
        // paths via IsNetworkPath, so this must resolve for NFS / SMB backends
        // too, not just LocalStorage.
        List<string> paths = library
            .FolderLibraries.Select(selector: folderLibrary =>
                StorageFactory
                    .For(folderId: folderLibrary.Folder.Id, driverId: folderLibrary.Folder.DriverId, subPath: string.Empty)
                    .Driver.GetFullPath(path: folderLibrary.Folder.Path)
            )
            .ToList();

        List<Action> disposers = [];

        Task.Run(action: () =>
            {
                disposers = Fs.Watch(paths: paths);
            })
            .Wait();

        return () =>
        {
            foreach (Action disposer in disposers)
                disposer();
        };
    }

    private void _onFileChanged(FileWatcherEventArgs e) => HandleFileChange(e: e);

    private void _onFileCreated(FileWatcherEventArgs e) => HandleFileChange(e: e);

    private void _onFileDeleted(FileWatcherEventArgs e) => HandleFileChange(e: e);

    private void _onFileRenamed(FileWatcherEventArgs e) => HandleFileChange(e: e);

    private void _onError(FileWatcherEventArgs e)
    {
        Logger.System(message: e, level: LogEventLevel.Error);
    }

    private static Library? GetLibraryByPath(string path)
    {
        return _libraries.FirstOrDefault(predicate: library =>
            library.FolderLibraries.Any(predicate: folderLibrary =>
            {
                // Resolve through the driver, not the IStorage facade: the
                // facade's GetFullPath is a LocalStorage-only escape hatch that
                // throws on every remote backend, so a facade call here killed
                // folder matching for NFS / SMB libraries.
                string driverRoot = StorageFactory
                    .For(folderId: folderLibrary.Folder.Id, driverId: folderLibrary.Folder.DriverId, subPath: string.Empty)
                    .Driver.GetFullPath(path: folderLibrary.Folder.Path);
                return path.StartsWith(value: driverRoot, comparisonType: StringComparison.OrdinalIgnoreCase);
            })
        );
    }

    private static bool IsInEncodingOutputDirectory(string fullPath)
    {
        string? directory = Path.GetDirectoryName(path: fullPath);
        while (!string.IsNullOrEmpty(value: directory))
        {
            string dirName = Path.GetFileName(path: directory);
            if (EncodingOutputRegex.IsMatch(input: dirName))
                return true;

            directory = Path.GetDirectoryName(path: directory);
        }
        return false;
    }

    private void HandleFileChange(FileWatcherEventArgs e)
    {
        if (IsInEncodingOutputDirectory(fullPath: e.FullPath))
            return;

        string watcherPath = e.Path;
        Library? library = GetLibraryByPath(path: watcherPath);

        if (library is null)
            return;

        if (!IsAllowedExtensionForLibrary(library: library, path: e.FullPath))
            return;

        if (e.ChangeType != WatcherChangeTypes.Deleted && !Path.Exists(path: e.FullPath))
            return;

        string folderPath = Path.GetDirectoryName(path: e.FullPath).OrEmpty();

        if (string.IsNullOrEmpty(value: folderPath))
            return;

        lock (LockObject)
        {
            if (!FileChangeGroups.TryGetValue(key: folderPath, value: out FileChangeGroup? fileChangeGroup))
            {
                fileChangeGroup = new(type: e.ChangeType, library: library, folderPath: folderPath);
                FileChangeGroups[key: folderPath] = fileChangeGroup;
            }

            fileChangeGroup.FullPath = e.FullPath;
            fileChangeGroup.ChangeType = e.ChangeType;

            if (e is { ChangeType: WatcherChangeTypes.Renamed, OldFullPath: not null })
                fileChangeGroup.OldFullPath = e.OldFullPath;

            fileChangeGroup.Timer?.Dispose();
            fileChangeGroup.Timer = new(
                callback: ProcessFileChanges,
                state: fileChangeGroup,
                dueTime: TimeSpan.FromSeconds(seconds: Delay),
                period: Timeout.InfiniteTimeSpan
            );
        }
    }

    private static bool IsAllowedExtensionForLibrary(Library library, string path)
    {
        if (StorageDriver.DirectoryExists(path: path))
            return true;

        switch (library.Type)
        {
            case MediaTypes.MovieMediaType:
            case MediaTypes.TvMediaType:
            case MediaTypes.AnimeMediaType:
                string[] videoExtensions = [".mp4", ".mkv", ".avi", ".webm", ".mov", ".m3u8"];
                return videoExtensions.Contains(
                    value: Path.GetExtension(path: path),
                    comparer: StringComparer.OrdinalIgnoreCase
                );
            case MediaTypes.MusicMediaType:
                string[] audioExtensions = [".mp3", ".flac", ".opus", ".wav", ".m4a"];
                return audioExtensions.Contains(
                    value: Path.GetExtension(path: path),
                    comparer: StringComparer.OrdinalIgnoreCase
                );
            case MediaTypes.InboxMediaType:
                string[] inboxExtensions =
                [
                    ".mp4",
                    ".mkv",
                    ".avi",
                    ".webm",
                    ".mov",
                    ".m3u8",
                    ".mp3",
                    ".flac",
                    ".opus",
                    ".wav",
                    ".m4a",
                ];
                return inboxExtensions.Contains(
                    value: Path.GetExtension(path: path),
                    comparer: StringComparer.OrdinalIgnoreCase
                );
            default:
                return false;
        }
    }

    private void ProcessFileChanges(object? state)
    {
        if (state is not FileChangeGroup group)
            return;

        FileChangeGroup snapshot;
        lock (LockObject)
        {
            snapshot = new(type: group.ChangeType, library: group.Library, folderPath: group.FolderPath)
            {
                FullPath = group.FullPath,
                OldFullPath = group.OldFullPath,
            };
            FileChangeGroups.Remove(key: group.FolderPath);
            // The one-shot debounce timer has fired; dispose it so its handle is
            // released now instead of leaking until GC on a busy library.
            group.Timer?.Dispose();
        }

        Task.Run(function: async () =>
        {
            try
            {
                await PublishFileEvent(group: snapshot);
            }
            catch (Exception ex)
            {
                Logger.System(
                    message: $"FileWatcher error processing {snapshot.FolderPath}: {ex.Message}",
                    level: LogEventLevel.Error
                );
            }
        });
    }

    private static async Task PublishFileEvent(FileChangeGroup group)
    {
        if (!EventBusProvider.IsConfigured)
            return;

        switch (group.ChangeType)
        {
            case WatcherChangeTypes.Created:
            case WatcherChangeTypes.Changed:
                Logger.System(
                    message: $"FileWatcher: Publishing FileCreatedEvent for {group.FolderPath}",
                    level: LogEventLevel.Debug
                );
                await EventBusProvider.Current.PublishAsync(
                    @event: new FileCreatedEvent
                    {
                        FolderPath = group.FolderPath,
                        LibraryId = group.Library.Id,
                        LibraryType = group.Library.Type,
                    }
                );
                break;

            case WatcherChangeTypes.Deleted:
                Logger.System(
                    message: $"FileWatcher: Publishing FileDeletedEvent for {group.FullPath}",
                    level: LogEventLevel.Debug
                );
                await EventBusProvider.Current.PublishAsync(
                    @event: new FileDeletedEvent
                    {
                        FullPath = group.FullPath ?? group.FolderPath,
                        LibraryId = group.Library.Id,
                        LibraryType = group.Library.Type,
                    }
                );
                break;

            case WatcherChangeTypes.Renamed when group.OldFullPath is not null:
                Logger.System(
                    message: $"FileWatcher: Publishing FileRenamedEvent from {group.OldFullPath} to {group.FullPath}",
                    level: LogEventLevel.Debug
                );
                await EventBusProvider.Current.PublishAsync(
                    @event: new FileRenamedEvent
                    {
                        OldFullPath = group.OldFullPath,
                        NewFullPath = group.FullPath ?? group.FolderPath,
                        LibraryId = group.Library.Id,
                        LibraryType = group.Library.Type,
                    }
                );
                break;

            case WatcherChangeTypes.Renamed:
                Logger.System(
                    message: $"FileWatcher: Rename detected but no OldFullPath, treating as Created for {group.FolderPath}",
                    level: LogEventLevel.Debug
                );
                await EventBusProvider.Current.PublishAsync(
                    @event: new FileCreatedEvent
                    {
                        FolderPath = group.FolderPath,
                        LibraryId = group.Library.Id,
                        LibraryType = group.Library.Type,
                    }
                );
                break;
        }
    }

    public static void Start(IStorageDriver storageDriver)
    {
        _driverStore = storageDriver;
        _ = Instance;
    }
}
