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

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.NmSystem.Domain;
using NoMercy.Storage;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Reclaim;

public sealed class ReclaimScanService : IReclaimScanService
{
    private const string PartialStaleHoursKey = "reclaim.partial_stale_hours";
    private const int DefaultPartialStaleHours = 6;

    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly IStorageFactory _storageFactory;
    private readonly IConfigurationStore _configurationStore;
    private readonly ILogger<ReclaimScanService> _logger;
    private readonly Func<Ulid, Ulid, string, IReadOnlyList<FolderEntry>> _listFolderEntries;

    private readonly object _gate = new();
    private ReclaimScanState _state = ReclaimScanState.Idle;
    private DateTimeOffset? _lastScannedAt;
    private ReclaimScanResult? _latest;
    private int _scanning;

    public ReclaimScanService(
        IDbContextFactory<MediaContext> contextFactory,
        IStorageFactory storageFactory,
        IConfigurationStore configurationStore,
        ILogger<ReclaimScanService> logger
    )
        : this(contextFactory: contextFactory, storageFactory: storageFactory, configurationStore: configurationStore, logger: logger, listFolderEntriesOverride: null) { }

    internal ReclaimScanService(
        IDbContextFactory<MediaContext> contextFactory,
        IStorageFactory storageFactory,
        IConfigurationStore configurationStore,
        ILogger<ReclaimScanService> logger,
        Func<Ulid, Ulid, string, IReadOnlyList<FolderEntry>>? listFolderEntriesOverride
    )
    {
        _contextFactory = contextFactory;
        _storageFactory = storageFactory;
        _configurationStore = configurationStore;
        _logger = logger;
        _listFolderEntries = listFolderEntriesOverride ?? ListFolderEntriesFromStorage;
    }

    public ReclaimScanState State
    {
        get
        {
            lock (_gate)
                return _state;
        }
        private set
        {
            lock (_gate)
                _state = value;
        }
    }

    public DateTimeOffset? LastScannedAt
    {
        get
        {
            lock (_gate)
                return _lastScannedAt;
        }
        private set
        {
            lock (_gate)
                _lastScannedAt = value;
        }
    }

    public ReclaimScanResult? Latest
    {
        get
        {
            lock (_gate)
                return _latest;
        }
        private set
        {
            lock (_gate)
                _latest = value;
        }
    }

    public Task StartScanAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(location1: ref _scanning, value: 1, comparand: 0) != 0)
            return Task.CompletedTask;

        State = ReclaimScanState.Scanning;

        // The scan is detached from the caller's request lifetime: it must keep
        // running to completion even after the triggering HTTP request returns
        // and its token cancels, so the background chain always runs under
        // CancellationToken.None rather than the caller's ct.
        _ = Task.Run(function: () => RunScanAsync(ct: CancellationToken.None), cancellationToken: CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task<long> DeleteItemAsync(string itemId, CancellationToken ct)
    {
        ReclaimableItem item = FindItemOrThrow(itemId: itemId);

        await using MediaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken: ct);

        FreshFolderDbInfo? dbInfo = await ResolveFreshFolderDbContextAsync(
            context: context,
            hostFolder: item.Folder,
            ct: ct
        );
        if (dbInfo is null)
            throw new InvalidOperationException(
                message: $"Folder '{item.Folder}' no longer resolves to a served copy; refusing to delete."
            );

        EnsureNoServedCopyConflict(item: item, freshRows: dbInfo.Value.Rows);

        IStorage storage = _storageFactory.For(
            folderId: dbInfo.Value.FolderId,
            driverId: dbInfo.Value.DriverId,
            subPath: string.Empty
        );
        IReadOnlyList<StorageEntry> freshEntries = storage.List(
            path: item.Folder,
            pattern: null,
            recursive: false
        );

        ReclaimClassification fresh = ClassifyFreshFolderState(
            storage: storage,
            freshEntries: freshEntries,
            freshRows: dbInfo.Value.Rows
        );

        if (fresh.Kind != ReclaimKind.ReclaimableHls)
        {
            _logger.LogWarning(
                message: "[ReclaimScanService] Folder {Folder} is no longer reclaimable-HLS (now {Kind}) — refusing to delete item {ItemId}", args: [item.Folder, fresh.Kind, item.Id]
            );
            throw new InvalidOperationException(
                message: $"Folder '{item.Folder}' is no longer reclaimable — original missing or folder now protected."
            );
        }

        IReadOnlyList<string> confirmedTargets = fresh
            .TargetNames.Select(selector: name => StoragePathHelpers.Combine(parent: item.Folder, child: name))
            .ToList();

        long freedBytes = DeleteTargets(storage: storage, freshEntries: freshEntries, targetPaths: confirmedTargets);

        RemoveItemFromSnapshot(itemId: itemId);

        return freedBytes;
    }

    private ReclaimClassification ClassifyFreshFolderState(
        IStorage storage,
        IReadOnlyList<StorageEntry> freshEntries,
        IReadOnlyList<VideoFileScanRow> freshRows
    )
    {
        bool isProtected = freshRows.Any(predicate: row => IsServedPlaylist(filename: row.Filename));

        List<FolderEntry> freshFolderEntries = freshEntries
            .Select(selector: entry => new FolderEntry(
                Name: storage.GetName(path: entry.Path),
                IsDirectory: entry.IsDirectory,
                Size: entry.SizeBytes,
                LastModified: entry.LastModified
            ))
            .ToList();

        return ReclaimClassifier.Classify(
            entries: freshFolderEntries,
            isProtected: isProtected,
            now: DateTimeOffset.UtcNow,
            partialStaleAfter: ResolvePartialStaleAfter()
        );
    }

    public async Task<(int count, long bytes)> SweepPartialsAsync(CancellationToken ct)
    {
        List<PartialJunkItem> partials;
        lock (_gate)
            partials = _latest?.PartialJunk.ToList() ?? [];

        if (partials.Count == 0)
            return (0, 0);

        await using MediaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken: ct);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan partialStaleAfter = ResolvePartialStaleAfter();

        List<string> sweptFolders = [];
        long freedBytes = 0;

        foreach (PartialJunkItem partial in partials)
        {
            ct.ThrowIfCancellationRequested();

            long? swept = await SweepPartialFolderAsync(
                context: context,
                partial: partial,
                now: now,
                partialStaleAfter: partialStaleAfter,
                ct: ct
            );
            if (swept is null)
                continue;

            freedBytes += swept.Value;
            sweptFolders.Add(item: partial.Folder);
        }

        if (sweptFolders.Count > 0)
            RemoveSweptPartialsFromSnapshot(sweptFolders: sweptFolders);

        return (sweptFolders.Count, freedBytes);
    }

    private ReclaimableItem FindItemOrThrow(string itemId)
    {
        lock (_gate)
        {
            ReclaimableItem? found = _latest?.Items.FirstOrDefault(predicate: candidate =>
                candidate.Id == itemId
            );
            if (found is null)
                throw new KeyNotFoundException(message: $"Reclaimable item '{itemId}' not found.");
            return found;
        }
    }

    private void EnsureNoServedCopyConflict(
        ReclaimableItem item,
        IReadOnlyList<VideoFileScanRow> freshRows
    )
    {
        foreach (VideoFileScanRow row in freshRows)
        {
            string servedPath = StoragePathHelpers.Combine(parent: item.Folder, child: row.Filename);

            foreach (string targetPath in item.TargetPaths)
            {
                if (!ConflictsWithServedCopy(targetPath: targetPath, servedPath: servedPath))
                    continue;

                _logger.LogWarning(
                    message: "[ReclaimScanService] Refusing to delete {TargetPath} for item {ItemId} — it matches the currently served copy {ServedPath}", args: [targetPath, item.Id, servedPath]
                );
                throw new InvalidOperationException(
                    message: $"Refusing to delete '{targetPath}' — it is the currently served copy '{servedPath}'."
                );
            }
        }
    }

    private static bool ConflictsWithServedCopy(string targetPath, string servedPath)
    {
        string normalizedTarget = targetPath.TrimEnd(trimChar: '/');
        string normalizedServed = servedPath.TrimEnd(trimChar: '/');

        if (string.Equals(a: normalizedTarget, b: normalizedServed, comparisonType: StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalizedServed.StartsWith(value: normalizedTarget + "/", comparisonType: StringComparison.OrdinalIgnoreCase))
            return true;

        return normalizedTarget.StartsWith(
            value: normalizedServed + "/",
            comparisonType: StringComparison.OrdinalIgnoreCase
        );
    }

    private static long DeleteTargets(
        IStorage storage,
        IReadOnlyList<StorageEntry> freshEntries,
        IReadOnlyList<string> targetPaths
    )
    {
        long freedBytes = 0;
        foreach (string targetPath in targetPaths)
        {
            StorageEntry? entry = freshEntries.FirstOrDefault(predicate: candidate =>
                candidate.Path == targetPath
            );
            if (entry is null)
                continue;

            if (entry.IsDirectory)
                storage.DeleteDirectory(path: targetPath, recursive: true);
            else
                storage.Delete(path: targetPath);

            freedBytes += entry.SizeBytes;
        }

        return freedBytes;
    }

    private void RemoveItemFromSnapshot(string itemId)
    {
        lock (_gate)
        {
            if (_latest is null)
                return;

            List<ReclaimableItem> remaining = _latest
                .Items.Where(predicate: candidate => candidate.Id != itemId)
                .ToList();
            _latest = _latest with
            {
                Items = remaining,
                TotalReclaimableBytes = remaining.Sum(selector: candidate => candidate.ReclaimableBytes),
            };
        }
    }

    private async Task<long?> SweepPartialFolderAsync(
        MediaContext context,
        PartialJunkItem partial,
        DateTimeOffset now,
        TimeSpan partialStaleAfter,
        CancellationToken ct
    )
    {
        FreshFolderDbInfo? dbInfo = await ResolveFreshFolderDbContextAsync(
            context: context,
            hostFolder: partial.Folder,
            ct: ct
        );
        if (dbInfo is null)
            return null;

        IStorage storage;
        IReadOnlyList<StorageEntry> freshEntries;
        try
        {
            storage = _storageFactory.For(
                folderId: dbInfo.Value.FolderId,
                driverId: dbInfo.Value.DriverId,
                subPath: string.Empty
            );
            freshEntries = storage.List(path: partial.Folder, pattern: null, recursive: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                exception: ex,
                message: "[ReclaimScanService] Could not re-list {Folder} during sweep — skipping",
                args: partial.Folder
            );
            return null;
        }

        bool isProtected = dbInfo.Value.Rows.Any(predicate: row => IsServedPlaylist(filename: row.Filename));

        List<FolderEntry> freshFolderEntries = freshEntries
            .Select(selector: entry => new FolderEntry(
                Name: storage.GetName(path: entry.Path),
                IsDirectory: entry.IsDirectory,
                Size: entry.SizeBytes,
                LastModified: entry.LastModified
            ))
            .ToList();

        ReclaimClassification classification = ReclaimClassifier.Classify(
            entries: freshFolderEntries,
            isProtected: isProtected,
            now: now,
            partialStaleAfter: partialStaleAfter
        );

        if (classification.Kind != ReclaimKind.OrphanPartial)
        {
            _logger.LogInformation(
                message: "[ReclaimScanService] Folder {Folder} is no longer a stale masterless orphan — skipping sweep",
                args: partial.Folder
            );
            return null;
        }

        IReadOnlyList<string> confirmedTargets = classification
            .TargetNames.Select(selector: name => StoragePathHelpers.Combine(parent: partial.Folder, child: name))
            .ToList();

        return DeleteTargets(storage: storage, freshEntries: freshEntries, targetPaths: confirmedTargets);
    }

    private void RemoveSweptPartialsFromSnapshot(List<string> sweptFolders)
    {
        lock (_gate)
        {
            if (_latest is null)
                return;

            List<PartialJunkItem> remaining = _latest
                .PartialJunk.Where(predicate: partial => !sweptFolders.Contains(item: partial.Folder))
                .ToList();

            _latest = _latest with
            {
                PartialJunk = remaining,
                TotalPartialJunkBytes = remaining.Sum(selector: partial => partial.Bytes),
            };
        }
    }

    private async Task<FreshFolderDbInfo?> ResolveFreshFolderDbContextAsync(
        MediaContext context,
        string hostFolder,
        CancellationToken ct
    )
    {
        List<VideoFileScanRow> freshRows = await context
            .VideoFiles.AsNoTracking()
            .Where(predicate: videoFile => videoFile.HostFolder == hostFolder)
            .Select(selector: videoFile => new VideoFileScanRow(
                videoFile.HostFolder,
                videoFile.Filename,
                videoFile.Share,
                videoFile.MovieId,
                videoFile.EpisodeId,
                null,
                null,
                null,
                null,
                null
            ))
            .ToListAsync(cancellationToken: ct);

        if (freshRows.Count == 0)
        {
            _logger.LogWarning(
                message: "[ReclaimScanService] Folder {HostFolder} no longer has any VideoFile rows — refusing to act",
                args: hostFolder
            );
            return null;
        }

        if (!Ulid.TryParse(base32: freshRows[index: 0].Share, ulid: out Ulid folderId))
        {
            _logger.LogWarning(
                message: "[ReclaimScanService] VideoFile share for {HostFolder} is not a folder id — refusing to act",
                args: hostFolder
            );
            return null;
        }

        Dictionary<Ulid, Ulid> driverIdByFolderId = await ResolveDriverIdsAsync(
            context: context,
            rows: freshRows,
            ct: ct
        );
        if (!driverIdByFolderId.TryGetValue(key: folderId, value: out Ulid driverId))
        {
            _logger.LogWarning(
                message: "[ReclaimScanService] Folder {FolderId} for {HostFolder} not found — refusing to act", args: [folderId, hostFolder]
            );
            return null;
        }

        return new FreshFolderDbInfo(Rows: freshRows, FolderId: folderId, DriverId: driverId);
    }

    private async Task RunScanAsync(CancellationToken ct)
    {
        try
        {
            ReclaimScanResult result = await ScanAsync(ct: ct);
            Latest = result;
            State = ReclaimScanState.Completed;
            LastScannedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(exception: ex, message: "[ReclaimScanService] Scan failed");
            State = ReclaimScanState.Failed;
        }
        finally
        {
            Interlocked.Exchange(location1: ref _scanning, value: 0);
        }
    }

    private async Task<ReclaimScanResult> ScanAsync(CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan partialStaleAfter = ResolvePartialStaleAfter();

        await using MediaContext context = await _contextFactory.CreateDbContextAsync(cancellationToken: ct);

        List<VideoFileScanRow> rows = await context
            .VideoFiles.AsNoTracking()
            .Select(selector: videoFile => new VideoFileScanRow(
                videoFile.HostFolder,
                videoFile.Filename,
                videoFile.Share,
                videoFile.MovieId,
                videoFile.EpisodeId,
                videoFile.Movie != null ? videoFile.Movie.Title : null,
                videoFile.Episode != null ? videoFile.Episode.Tv.Title : null,
                videoFile.Episode != null ? videoFile.Episode.Tv.MediaType : null,
                videoFile.Episode != null ? (int?)videoFile.Episode.SeasonNumber : null,
                videoFile.Episode != null ? (int?)videoFile.Episode.EpisodeNumber : null
            ))
            .ToListAsync(cancellationToken: ct);

        Dictionary<Ulid, Ulid> driverIdByFolderId = await ResolveDriverIdsAsync(context: context, rows: rows, ct: ct);

        List<ReclaimableItem> items = [];
        List<PartialJunkItem> partialJunk = [];

        foreach (IGrouping<string, VideoFileScanRow> group in rows.GroupBy(keySelector: row => row.HostFolder))
        {
            string hostFolder = group.Key;
            VideoFileScanRow firstRow = group.First();

            if (!Ulid.TryParse(base32: firstRow.Share, ulid: out Ulid folderId))
            {
                _logger.LogWarning(
                    message: "[ReclaimScanService] VideoFile share '{Share}' in folder {HostFolder} is not a folder id — skipping", args: [firstRow.Share, hostFolder]
                );
                continue;
            }

            if (!driverIdByFolderId.TryGetValue(key: folderId, value: out Ulid driverId))
            {
                _logger.LogWarning(
                    message: "[ReclaimScanService] Folder {FolderId} for {HostFolder} not found — skipping", args: [folderId, hostFolder]
                );
                continue;
            }

            bool isProtected;
            VideoFileScanRow servedRow;
            IReadOnlyList<FolderEntry> entries;
            try
            {
                isProtected = group.Any(predicate: row => IsServedPlaylist(filename: row.Filename));

                servedRow = group.FirstOrDefault(predicate: row => IsServedPlaylist(filename: row.Filename)) ?? firstRow;

                entries = _listFolderEntries(arg1: folderId, arg2: driverId, arg3: hostFolder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    exception: ex,
                    message: "[ReclaimScanService] Could not process {HostFolder} — skipping",
                    args: hostFolder
                );
                continue;
            }

            ReclaimClassification classification = ReclaimClassifier.Classify(
                entries: entries,
                isProtected: isProtected,
                now: now,
                partialStaleAfter: partialStaleAfter
            );

            if (classification.Kind == ReclaimKind.None)
                continue;

            IReadOnlyList<string> targetPaths = classification
                .TargetNames.Select(selector: name => StoragePathHelpers.Combine(parent: hostFolder, child: name))
                .ToList();

            if (classification.Kind == ReclaimKind.ReclaimableHls)
            {
                items.Add(
                    item: new(
                        Id: DeterministicId(hostFolder: hostFolder),
                        Title: ResolveTitle(row: servedRow, hostFolder: hostFolder),
                        MediaType: ResolveMediaType(row: servedRow),
                        Folder: hostFolder,
                        ServedCopy: servedRow.Filename,
                        Kind: classification.Kind,
                        TargetPaths: targetPaths,
                        ReclaimableBytes: classification.ReclaimableBytes
                    )
                );
            }
            else
            {
                partialJunk.Add(item: new(Folder: hostFolder, TargetPaths: targetPaths, Bytes: classification.ReclaimableBytes));
            }
        }

        return new(
            Items: items,
            PartialJunk: partialJunk,
            TotalReclaimableBytes: items.Sum(selector: item => item.ReclaimableBytes),
            TotalPartialJunkBytes: partialJunk.Sum(selector: item => item.Bytes)
        );
    }

    private static async Task<Dictionary<Ulid, Ulid>> ResolveDriverIdsAsync(
        MediaContext context,
        List<VideoFileScanRow> rows,
        CancellationToken ct
    )
    {
        HashSet<Ulid> folderIds = [];
        foreach (VideoFileScanRow row in rows)
        {
            if (Ulid.TryParse(base32: row.Share, ulid: out Ulid folderId))
                folderIds.Add(item: folderId);
        }

        if (folderIds.Count == 0)
            return [];

        List<Folder> folders = await context
            .Folders.AsNoTracking()
            .Where(predicate: folder => folderIds.Contains(folder.Id))
            .ToListAsync(cancellationToken: ct);

        return folders.ToDictionary(keySelector: folder => folder.Id, elementSelector: folder => folder.DriverId);
    }

    private TimeSpan ResolvePartialStaleAfter()
    {
        string? raw = _configurationStore.GetValue(key: PartialStaleHoursKey);
        if (
            !string.IsNullOrWhiteSpace(value: raw)
            && double.TryParse(
                s: raw,
                style: NumberStyles.Float | NumberStyles.AllowThousands,
                provider: CultureInfo.InvariantCulture,
                result: out double hours
            )
            && hours > 0
        )
            return TimeSpan.FromHours(value: hours);

        return TimeSpan.FromHours(hours: DefaultPartialStaleHours);
    }

    private IReadOnlyList<FolderEntry> ListFolderEntriesFromStorage(
        Ulid folderId,
        Ulid driverId,
        string hostFolder
    )
    {
        IStorage storage = _storageFactory.For(folderId: folderId, driverId: driverId, subPath: string.Empty);
        IReadOnlyList<StorageEntry> entries = storage.List(path: hostFolder, pattern: null, recursive: false);

        return entries
            .Select(selector: entry => new FolderEntry(
                Name: storage.GetName(path: entry.Path),
                IsDirectory: entry.IsDirectory,
                Size: entry.SizeBytes,
                LastModified: entry.LastModified
            ))
            .ToList();
    }

    private static string ResolveTitle(VideoFileScanRow row, string hostFolder)
    {
        if (row.MovieId is not null && !string.IsNullOrEmpty(value: row.MovieTitle))
            return row.MovieTitle;

        if (row.EpisodeId is not null && !string.IsNullOrEmpty(value: row.ShowTitle))
        {
            return row.SeasonNumber is not null && row.EpisodeNumber is not null
                ? $"{row.ShowTitle} S{row.SeasonNumber:00}E{row.EpisodeNumber:00}"
                : row.ShowTitle;
        }

        return LeafName(hostFolder: hostFolder);
    }

    private static string ResolveMediaType(VideoFileScanRow row)
    {
        if (row.MovieId is not null)
            return MediaTypes.MovieMediaType;

        if (row.EpisodeId is not null)
            return row.ShowMediaType ?? MediaTypes.TvMediaType;

        return "unknown";
    }

    private static string LeafName(string hostFolder)
    {
        string trimmed = hostFolder.TrimEnd(trimChar: '/');
        int idx = trimmed.LastIndexOf(value: '/');
        return idx < 0 ? trimmed : trimmed[(idx + 1)..];
    }

    private static bool IsServedPlaylist(string? filename) =>
        !string.IsNullOrEmpty(value: filename)
        && filename.EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase);

    private static string DeterministicId(string hostFolder)
    {
        byte[] hash = SHA256.HashData(source: Encoding.UTF8.GetBytes(s: hostFolder));
        return new Ulid(bytes: hash.AsSpan(start: 0, length: 16).ToArray()).ToString();
    }

    private sealed record VideoFileScanRow(
        string HostFolder,
        string Filename,
        string Share,
        int? MovieId,
        int? EpisodeId,
        string? MovieTitle,
        string? ShowTitle,
        string? ShowMediaType,
        int? SeasonNumber,
        int? EpisodeNumber
    );

    private readonly record struct FreshFolderDbInfo(
        IReadOnlyList<VideoFileScanRow> Rows,
        Ulid FolderId,
        Ulid DriverId
    );
}
