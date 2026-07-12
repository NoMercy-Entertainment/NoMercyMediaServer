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
        : this(contextFactory, storageFactory, configurationStore, logger, null) { }

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
        if (Interlocked.CompareExchange(ref _scanning, 1, 0) != 0)
            return Task.CompletedTask;

        State = ReclaimScanState.Scanning;

        // The scan is detached from the caller's request lifetime: it must keep
        // running to completion even after the triggering HTTP request returns
        // and its token cancels, so the background chain always runs under
        // CancellationToken.None rather than the caller's ct.
        _ = Task.Run(() => RunScanAsync(CancellationToken.None), CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task<long> DeleteItemAsync(string itemId, CancellationToken ct) =>
        throw new NotImplementedException("Reclaim delete lands in Task 3.");

    public Task<(int count, long bytes)> SweepPartialsAsync(CancellationToken ct) =>
        throw new NotImplementedException("Reclaim sweep lands in Task 3.");

    private async Task RunScanAsync(CancellationToken ct)
    {
        try
        {
            ReclaimScanResult result = await ScanAsync(ct);
            Latest = result;
            State = ReclaimScanState.Completed;
            LastScannedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ReclaimScanService] Scan failed");
            State = ReclaimScanState.Failed;
        }
        finally
        {
            Interlocked.Exchange(ref _scanning, 0);
        }
    }

    private async Task<ReclaimScanResult> ScanAsync(CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan partialStaleAfter = ResolvePartialStaleAfter();

        await using MediaContext context = await _contextFactory.CreateDbContextAsync(ct);

        List<VideoFileScanRow> rows = await context
            .VideoFiles.AsNoTracking()
            .Select(videoFile => new VideoFileScanRow(
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
            .ToListAsync(ct);

        Dictionary<Ulid, Ulid> driverIdByFolderId = await ResolveDriverIdsAsync(context, rows, ct);

        List<ReclaimableItem> items = [];
        List<PartialJunkItem> partialJunk = [];

        foreach (IGrouping<string, VideoFileScanRow> group in rows.GroupBy(row => row.HostFolder))
        {
            string hostFolder = group.Key;
            VideoFileScanRow firstRow = group.First();

            if (!Ulid.TryParse(firstRow.Share, out Ulid folderId))
            {
                _logger.LogWarning(
                    "[ReclaimScanService] VideoFile share '{Share}' in folder {HostFolder} is not a folder id — skipping",
                    firstRow.Share,
                    hostFolder
                );
                continue;
            }

            if (!driverIdByFolderId.TryGetValue(folderId, out Ulid driverId))
            {
                _logger.LogWarning(
                    "[ReclaimScanService] Folder {FolderId} for {HostFolder} not found — skipping",
                    folderId,
                    hostFolder
                );
                continue;
            }

            bool isProtected;
            VideoFileScanRow servedRow;
            IReadOnlyList<FolderEntry> entries;
            try
            {
                isProtected = group.Any(row => IsServedPlaylist(row.Filename));

                servedRow = group.FirstOrDefault(row => IsServedPlaylist(row.Filename)) ?? firstRow;

                entries = _listFolderEntries(folderId, driverId, hostFolder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[ReclaimScanService] Could not process {HostFolder} — skipping",
                    hostFolder
                );
                continue;
            }

            ReclaimClassification classification = ReclaimClassifier.Classify(
                entries,
                isProtected,
                now,
                partialStaleAfter
            );

            if (classification.Kind == ReclaimKind.None)
                continue;

            IReadOnlyList<string> targetPaths = classification
                .TargetNames.Select(name => StoragePathHelpers.Combine(hostFolder, name))
                .ToList();

            if (classification.Kind == ReclaimKind.ReclaimableHls)
            {
                items.Add(
                    new(
                        Id: DeterministicId(hostFolder),
                        Title: ResolveTitle(servedRow, hostFolder),
                        MediaType: ResolveMediaType(servedRow),
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
                partialJunk.Add(new(hostFolder, targetPaths, classification.ReclaimableBytes));
            }
        }

        return new(
            items,
            partialJunk,
            items.Sum(item => item.ReclaimableBytes),
            partialJunk.Sum(item => item.Bytes)
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
            if (Ulid.TryParse(row.Share, out Ulid folderId))
                folderIds.Add(folderId);
        }

        if (folderIds.Count == 0)
            return [];

        List<Folder> folders = await context
            .Folders.AsNoTracking()
            .Where(folder => folderIds.Contains(folder.Id))
            .ToListAsync(ct);

        return folders.ToDictionary(folder => folder.Id, folder => folder.DriverId);
    }

    private TimeSpan ResolvePartialStaleAfter()
    {
        string? raw = _configurationStore.GetValue(PartialStaleHoursKey);
        if (
            !string.IsNullOrWhiteSpace(raw)
            && double.TryParse(
                raw,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out double hours
            )
            && hours > 0
        )
            return TimeSpan.FromHours(hours);

        return TimeSpan.FromHours(DefaultPartialStaleHours);
    }

    private IReadOnlyList<FolderEntry> ListFolderEntriesFromStorage(
        Ulid folderId,
        Ulid driverId,
        string hostFolder
    )
    {
        IStorage storage = _storageFactory.For(folderId, driverId, string.Empty);
        IReadOnlyList<StorageEntry> entries = storage.List(hostFolder, null, recursive: false);

        return entries
            .Select(entry => new FolderEntry(
                storage.GetName(entry.Path),
                entry.IsDirectory,
                entry.SizeBytes,
                entry.LastModified
            ))
            .ToList();
    }

    private static string ResolveTitle(VideoFileScanRow row, string hostFolder)
    {
        if (row.MovieId is not null && !string.IsNullOrEmpty(row.MovieTitle))
            return row.MovieTitle;

        if (row.EpisodeId is not null && !string.IsNullOrEmpty(row.ShowTitle))
        {
            return row.SeasonNumber is not null && row.EpisodeNumber is not null
                ? $"{row.ShowTitle} S{row.SeasonNumber:00}E{row.EpisodeNumber:00}"
                : row.ShowTitle;
        }

        return LeafName(hostFolder);
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
        string trimmed = hostFolder.TrimEnd('/');
        int idx = trimmed.LastIndexOf('/');
        return idx < 0 ? trimmed : trimmed[(idx + 1)..];
    }

    private static bool IsServedPlaylist(string? filename) =>
        !string.IsNullOrEmpty(filename)
        && filename.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);

    private static string DeterministicId(string hostFolder)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(hostFolder));
        return new Ulid(hash.AsSpan(0, 16).ToArray()).ToString();
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
}
