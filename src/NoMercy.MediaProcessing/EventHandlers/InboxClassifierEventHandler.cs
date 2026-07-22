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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.Events.Inbox;
using NoMercy.MediaProcessing.Inbox;
using NoMercy.NmSystem.Domain;
using NoMercy.Storage;

namespace NoMercy.MediaProcessing.EventHandlers;

public class InboxClassifierEventHandler : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly InboxClassifier _classifier;
    private readonly InboxRoutingService _routing;
    private readonly Func<MediaContext> _contextFactory;
    private readonly IStorageFactory _storageFactory;
    private readonly List<IDisposable> _subscriptions = [];

    // Content-hash dedup keyed by (size, first-64KB MD5). Catches hard links and
    // duplicate copies that the SourcePath check misses. Per-process lifetime.
    private readonly ConcurrentDictionary<FileContentFingerprint, string> _seenContent = new();

    private readonly record struct FileContentFingerprint(long SizeBytes, string HashPrefix);

    private readonly ILogger<InboxClassifierEventHandler> _logger;

    public InboxClassifierEventHandler(
        ILogger<InboxClassifierEventHandler> logger,
        IEventBus eventBus,
        InboxClassifier classifier,
        InboxRoutingService routing,
        Func<MediaContext> contextFactory,
        IStorageFactory storageFactory
    )
    {
        _logger = logger;
        _eventBus = eventBus;
        _classifier = classifier;
        _routing = routing;
        _contextFactory = contextFactory;
        _storageFactory = storageFactory;
        _subscriptions.Add(item: eventBus.Subscribe<FileCreatedEvent>(handler: OnFileCreated));
    }

    private static async Task<FileContentFingerprint?> TryComputeFingerprintAsync(
        IStorage storage,
        string path,
        long sizeBytes,
        CancellationToken ct
    )
    {
        try
        {
            await using Stream stream = storage.OpenRead(path: path);
            int len = (int)Math.Min(val1: 65536, val2: sizeBytes <= 0 ? 65536 : sizeBytes);
            byte[] buffer = new byte[len];
            int read = await stream.ReadAsync(buffer: buffer.AsMemory(start: 0, length: len), cancellationToken: ct);
            string hash = Convert.ToHexString(inArray: MD5.HashData(source: buffer.AsSpan(start: 0, length: read)));
            return new FileContentFingerprint(SizeBytes: sizeBytes, HashPrefix: hash);
        }
        catch
        {
            // Can't fingerprint (directory, transient IO, permissions) — let the
            // item proceed rather than risk dropping a real file.
            return null;
        }
    }

    internal async Task OnFileCreated(FileCreatedEvent @event, CancellationToken ct)
    {
        if (@event.LibraryType != MediaTypes.InboxMediaType)
            return;

        _logger.LogInformation(
            message: "InboxClassifier: Processing drop event in {FolderPath}",
            args: @event.FolderPath
        );

        await using MediaContext context = _contextFactory();

        Library? library = await context
            .Libraries.AsNoTracking()
            .Include(navigationPropertyPath: l => l.FolderLibraries)
                .ThenInclude(navigationPropertyPath: fl => fl.Folder)
            .FirstOrDefaultAsync(predicate: l => l.Id == @event.LibraryId, cancellationToken: ct);

        if (library is null)
        {
            _logger.LogWarning(
                message: "InboxClassifier: Library {LibraryId} not found, dropping event",
                args: @event.LibraryId
            );
            return;
        }

        FolderLibrary? folderLibrary =
            library.FolderLibraries.FirstOrDefault(predicate: fl =>
                @event.FolderPath.StartsWith(value: fl.Folder.Path, comparisonType: StringComparison.OrdinalIgnoreCase)
            ) ?? library.FolderLibraries.FirstOrDefault();

        if (folderLibrary is null)
        {
            _logger.LogWarning(
                message: "InboxClassifier: No folder found for library {LibraryId}",
                args: @event.LibraryId
            );
            return;
        }

        string inboxRoot = folderLibrary.Folder.Path;
        Ulid folderId = folderLibrary.FolderId;
        Ulid driverId = folderLibrary.Folder.DriverId;

        IStorage storage = _storageFactory.For(folderId: folderId, driverId: driverId, subPath: inboxRoot);

        IReadOnlyList<StorageEntry> children = storage.List(path: "", pattern: null, recursive: false);

        if (children.Count == 0)
        {
            _logger.LogWarning(
                message: "InboxClassifier: No children found in inbox root {InboxRoot}",
                args: inboxRoot
            );
            return;
        }

        HashSet<string> tracked = await context
            .InboxItems.AsNoTracking()
            .Select(selector: item => item.SourcePath)
            .ToHashSetAsync(comparer: StringComparer.OrdinalIgnoreCase, cancellationToken: ct);

        foreach (StorageEntry child in children)
        {
            string childPath = storage.CombinePath(parent: inboxRoot, child: child.Path);

            if (tracked.Contains(item: childPath))
                continue;

            FileContentFingerprint? fingerprint = await TryComputeFingerprintAsync(
                storage: storage,
                path: childPath,
                sizeBytes: child.SizeBytes,
                ct: ct
            );
            if (fingerprint is { } fp && !_seenContent.TryAdd(key: fp, value: childPath))
            {
                _logger.LogInformation(
                    message: "InboxClassifier: skipping {ChildPath} — duplicate content already seen at {Fp}", args: [childPath, _seenContent[key: fp]]
                );
                continue;
            }

            _logger.LogInformation(
                message: "InboxClassifier: Classifying inbox child {ChildPath}",
                args: childPath
            );

            try
            {
                ClassificationResult classification = await _classifier.Classify(
                    path: childPath,
                    driverId: driverId,
                    ct: ct
                );
                RouteOutcome outcome = await _routing.Route(
                    classification: classification,
                    sourcePath: childPath,
                    driverId: driverId,
                    context: context,
                    ct: ct
                );

                if (outcome.Mode == "auto")
                {
                    await _routing.ExecuteAuto(outcome: outcome, context: context, ct: ct);
                }
                else
                {
                    InboxItem item = outcome.Item;
                    context.InboxItems.Add(entity: item);
                    await context.SaveChangesAsync(cancellationToken: ct);

                    await _eventBus.PublishAsync(
                        @event: new InboxItemDetectedEvent
                        {
                            Id = item.Id.ToString(),
                            DetectedType = item.DetectedType,
                            Confidence = item.Confidence,
                            Status = item.Status,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    message: "InboxClassifier: Error processing {ChildPath}: {Message}", args: [childPath, ex.Message]
                );

                InboxItem failedItem = new()
                {
                    Id = Ulid.NewUlid(),
                    SourcePath = childPath,
                    DriverId = driverId,
                    DetectedType = "unknown",
                    Status = "Failed",
                    Error = ex.Message,
                };

                try
                {
                    await using MediaContext failContext = _contextFactory();
                    failContext.InboxItems.Add(entity: failedItem);
                    await failContext.SaveChangesAsync(cancellationToken: ct);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(
                        message: "InboxClassifier: Could not persist Failed item for {ChildPath}: {Message}", args: [childPath, saveEx.Message]
                    );
                }

                await _eventBus.PublishAsync(
                    @event: new InboxItemDetectedEvent
                    {
                        Id = failedItem.Id.ToString(),
                        DetectedType = failedItem.DetectedType,
                        Confidence = "low",
                        Status = failedItem.Status,
                    }
                );
            }
        }
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
