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

using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;
using Stowage;

namespace NoMercy.MediaProcessing.Files;

public class FolderWatcher : IDisposable
{
    private static readonly Dictionary<string, IDisposable> Watchers = new(
        comparer: StringComparer.OrdinalIgnoreCase
    );
    private static readonly object WatchersLock = new();
    private static volatile FolderWatcher? _instance;
    private readonly IStorageDriver _storageDriver;

    public FolderWatcher(IStorageDriver storageDriver)
    {
        _storageDriver = storageDriver;
    }

    public event Action<FileWatcherEventArgs>? OnChanged;
    public event Action<FileWatcherEventArgs>? OnCreated;
    public event Action<FileWatcherEventArgs>? OnRenamed;
    public event Action<FileWatcherEventArgs>? OnDeleted;
    public event Action<FileWatcherEventArgs>? OnError;

    public List<Action> Watch(List<string> paths)
    {
        Interlocked.CompareExchange(location1: ref _instance, value: this, comparand: null);
        return WatchFolders(foldersToWatch: paths);
    }

    private List<Action> WatchFolders(List<string> foldersToWatch)
    {
        List<Action> disposers = [];
        disposers.AddRange(
            collection: from folder in foldersToWatch
            where _storageDriver.DirectoryExists(path: folder)
            select CreateWatcher(folder: folder)
        );

        return disposers;
    }

    private static Action CreateWatcher(string folder)
    {
        folder = Path.GetFullPath(path: folder);
        return !IsNetworkPath(path: folder)
            ? StartFileSystemWatcher(folder: folder)
            : StartNetworkFileWatcher(folder: folder);
    }

    private static bool IsNetworkPath(string path)
    {
        if (string.IsNullOrWhiteSpace(value: path))
            return false;
        if (path.StartsWith(value: @"\\"))
            return true; // UNC path
        string? drive = Path.GetPathRoot(path: path);
        if (string.IsNullOrEmpty(value: drive))
            return false;
        try
        {
            DriveInfo driveInfo = new(driveName: drive);
            return driveInfo.DriveType == DriveType.Network;
        }
        catch
        {
            return false;
        }
    }

    private static Action StartNetworkFileWatcher(string folder)
    {
        IFileStorage storage;
        if (
            folder.StartsWith(value: "s3://", comparisonType: StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith(value: "gs://", comparisonType: StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith(value: "az://", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            storage = Stowage.Files.Of.ConnectionString(connnectionString: folder);
        }
        else
        {
            storage = Stowage.Files.Of.ConnectionString(connnectionString: "disk://path=" + folder);
        }

        StowageWatcher stowageWatcher = new(storage: storage);
        stowageWatcher.Changed += e =>
        {
            _onFileChanged(sender: _instance!, e: e.ToFileSystemEventArgsEventArgs(folder: folder));
        };
        stowageWatcher.Created += e =>
        {
            _onFileCreated(sender: _instance!, e: e.ToFileSystemEventArgsEventArgs(folder: folder));
        };
        stowageWatcher.Deleted += e =>
        {
            _onFileDeleted(sender: _instance!, e: e.ToFileSystemEventArgsEventArgs(folder: folder));
        };
        stowageWatcher.Watch(interval: TimeSpan.FromMinutes(minutes: 1));

        RegisterWatcher(folder: folder, watcher: stowageWatcher);

        Logger.System(message: $"Watching folder: {folder}");

        return () =>
        {
            RemoveWatcher(folder: folder, watcher: stowageWatcher);
        };
    }

    private static Action StartFileSystemWatcher(string folder)
    {
        FileSystemWatcher fileSystemWatcher = new();
        fileSystemWatcher.Path = folder;
        fileSystemWatcher.EnableRaisingEvents = true;
        fileSystemWatcher.IncludeSubdirectories = true;
        fileSystemWatcher.NotifyFilter =
            // NotifyFilters.Attributes |
            // NotifyFilters.CreationTime |
            NotifyFilters.DirectoryName
            | NotifyFilters.FileName
            |
            // NotifyFilters.LastAccess |
            NotifyFilters.LastWrite
        // NotifyFilters.Security |
        // NotifyFilters.Size
        ;
        fileSystemWatcher.InternalBufferSize = 64 * 1024;

        fileSystemWatcher.Filter = "*.*";
        fileSystemWatcher.Changed -= _onFileChanged;
        fileSystemWatcher.Created -= _onFileCreated;
        fileSystemWatcher.Deleted -= _onFileDeleted;
        fileSystemWatcher.Renamed -= _onFileRenamed;
        fileSystemWatcher.Error -= _onError;

        fileSystemWatcher.Changed += _onFileChanged;
        fileSystemWatcher.Created += _onFileCreated;
        fileSystemWatcher.Deleted += _onFileDeleted;
        fileSystemWatcher.Renamed += _onFileRenamed;
        fileSystemWatcher.Error += _onError;

        fileSystemWatcher.EnableRaisingEvents = true;

        RegisterWatcher(folder: folder, watcher: fileSystemWatcher);

        Logger.System(message: $"Watching folder: {folder}");

        return () =>
        {
            RemoveWatcher(folder: folder, watcher: fileSystemWatcher);
        };
    }

    private string _prevChanged = "";

    private static void _onFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_instance is null)
            return;
        string current = e.FullPath + DateTime.UtcNow.ToString(format: "HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Changed || _instance._prevChanged == current)
            return;
        _instance._prevChanged = current;

        _instance.OnChanged?.Invoke(obj: new(sender: sender as FileSystemWatcher, fileSystemEventArgs: e));

        Logger.System(message: $"File Changed: {e.FullPath}", level: LogEventLevel.Verbose);
    }

    private string _prevCreated = "";

    private static void _onFileCreated(object sender, FileSystemEventArgs e)
    {
        if (_instance is null)
            return;
        string current = e.FullPath + DateTime.UtcNow.ToString(format: "HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Created || _instance._prevCreated == current)
            return;
        _instance._prevCreated = current;

        _instance.OnCreated?.Invoke(obj: new(sender: sender as FileSystemWatcher, fileSystemEventArgs: e));

        Logger.System(message: $"File Created: {e.FullPath}", level: LogEventLevel.Verbose);
    }

    private string _prevDeleted = "";

    private static void _onFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (_instance is null)
            return;
        string current = e.FullPath + DateTime.UtcNow.ToString(format: "HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Deleted || _instance._prevDeleted == current)
            return;
        _instance._prevDeleted = current;

        _instance.OnDeleted?.Invoke(obj: new(sender: sender as FileSystemWatcher, fileSystemEventArgs: e));

        Logger.System(message: $"File Deleted: {e.FullPath}", level: LogEventLevel.Verbose);
    }

    private string _prevRenamed = "";

    private static void _onFileRenamed(object sender, RenamedEventArgs e)
    {
        if (_instance is null)
            return;
        string current = e.FullPath + DateTime.UtcNow.ToString(format: "HHmmssddMMyyyy");

        if (e.ChangeType != WatcherChangeTypes.Renamed || _instance._prevRenamed == current)
            return;
        _instance._prevRenamed = current;

        _instance.OnRenamed?.Invoke(obj: new(sender: sender as FileSystemWatcher, fileSystemEventArgs: e));

        Logger.System(message: $"File Renamed from {e.OldFullPath} to {e.FullPath}", level: LogEventLevel.Verbose);
    }

    private static void _onError(object sender, ErrorEventArgs e)
    {
        FileWatcherEventArgs fileWatcherEventArgs = new(
            sender: sender as FileSystemWatcher,
            fileSystemEventArgs: new(changeType: WatcherChangeTypes.All, directory: "", name: "")
        )
        {
            ErrorEventArgs = e,
        };

        _instance?.OnError?.Invoke(obj: fileWatcherEventArgs);

        Logger.System(message: $"FolderWatcher error:  {e.GetException().Message}", level: LogEventLevel.Error);

        // A FileSystemWatcher stops raising events after an error (typically an
        // InternalBufferOverflow during a burst of changes). Without re-arming it
        // here the folder goes permanently blind and new media is never picked up
        // until the server restarts.
        TryReArm(watcher: sender as FileSystemWatcher);
    }

    public static bool TryReArm(FileSystemWatcher? watcher)
    {
        if (watcher is null)
            return false;

        try
        {
            watcher.EnableRaisingEvents = false;
            watcher.EnableRaisingEvents = true;
            Logger.System(
                message: $"FolderWatcher re-armed after error on {watcher.Path}",
                level: LogEventLevel.Warning
            );
            return true;
        }
        catch (Exception ex)
        {
            Logger.System(
                message: $"FolderWatcher failed to re-arm watcher: {ex.Message}",
                level: LogEventLevel.Error
            );
            return false;
        }
    }

    // Re-watching a folder (a library add / refresh / rescan calls Watch again) must
    // REPLACE that folder's watcher, not stack a second one: duplicates leak OS watch
    // handles + threads and make every file event fire twice → duplicate scan/encode
    // jobs. Keyed by folder so the set stays bounded to one watcher per folder.
    private static void RegisterWatcher(string folder, IDisposable watcher)
    {
        lock (WatchersLock)
        {
            if (Watchers.Remove(key: folder, value: out IDisposable? existing))
                existing.Dispose();
            Watchers[key: folder] = watcher;
        }
    }

    private static void RemoveWatcher(string folder, IDisposable watcher)
    {
        lock (WatchersLock)
        {
            if (
                Watchers.TryGetValue(key: folder, value: out IDisposable? current)
                && ReferenceEquals(objA: current, objB: watcher)
            )
                Watchers.Remove(key: folder);
        }

        watcher.Dispose();
    }

    internal static int WatcherCount
    {
        get
        {
            lock (WatchersLock)
                return Watchers.Count;
        }
    }

    public void Dispose()
    {
        lock (WatchersLock)
        {
            foreach (IDisposable watcher in Watchers.Values)
                watcher.Dispose();
            Watchers.Clear();
        }
    }
}
