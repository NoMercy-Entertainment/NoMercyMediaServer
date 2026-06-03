using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.Events.Inbox;
using NoMercy.MediaProcessing.Inbox;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.MediaProcessing.EventHandlers;

public class InboxClassifierEventHandler : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly InboxClassifier _classifier;
    private readonly InboxRoutingService _routing;
    private readonly Func<MediaContext> _contextFactory;
    private readonly IStorageFactory _storageFactory;
    private readonly List<IDisposable> _subscriptions = [];

    public InboxClassifierEventHandler(
        IEventBus eventBus,
        InboxClassifier classifier,
        InboxRoutingService routing,
        Func<MediaContext> contextFactory,
        IStorageFactory storageFactory
    )
    {
        _eventBus = eventBus;
        _classifier = classifier;
        _routing = routing;
        _contextFactory = contextFactory;
        _storageFactory = storageFactory;
        _subscriptions.Add(eventBus.Subscribe<FileCreatedEvent>(OnFileCreated));
    }

    internal async Task OnFileCreated(FileCreatedEvent @event, CancellationToken ct)
    {
        if (@event.LibraryType != Config.InboxMediaType)
            return;

        Logger.System($"InboxClassifier: Processing drop event in {@event.FolderPath}");

        await using MediaContext context = _contextFactory();

        Library? library = await context
            .Libraries.AsNoTracking()
            .Include(l => l.FolderLibraries)
                .ThenInclude(fl => fl.Folder)
            .FirstOrDefaultAsync(l => l.Id == @event.LibraryId, ct);

        if (library is null)
        {
            Logger.System(
                $"InboxClassifier: Library {@event.LibraryId} not found, dropping event",
                LogEventLevel.Warning
            );
            return;
        }

        FolderLibrary? folderLibrary =
            library.FolderLibraries.FirstOrDefault(fl =>
                @event.FolderPath.StartsWith(fl.Folder.Path, StringComparison.OrdinalIgnoreCase)
            ) ?? library.FolderLibraries.FirstOrDefault();

        if (folderLibrary is null)
        {
            Logger.System(
                $"InboxClassifier: No folder found for library {@event.LibraryId}",
                LogEventLevel.Warning
            );
            return;
        }

        string inboxRoot = folderLibrary.Folder.Path;
        Ulid folderId = folderLibrary.FolderId;
        Ulid driverId = folderLibrary.Folder.DriverId;

        IStorage storage = _storageFactory.For(folderId, driverId, inboxRoot);

        IReadOnlyList<StorageEntry> children = storage.List("", null, recursive: false);

        if (children.Count == 0)
        {
            Logger.System(
                $"InboxClassifier: No children found in inbox root {inboxRoot}",
                LogEventLevel.Warning
            );
            return;
        }

        HashSet<string> tracked = await context
            .InboxItems.AsNoTracking()
            .Select(item => item.SourcePath)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, ct);

        foreach (StorageEntry child in children)
        {
            string childPath = storage.CombinePath(inboxRoot, child.Path);

            if (tracked.Contains(childPath))
                continue;

            Logger.System($"InboxClassifier: Classifying inbox child {childPath}");

            try
            {
                ClassificationResult classification = await _classifier.Classify(
                    childPath,
                    driverId,
                    ct
                );
                RouteOutcome outcome = await _routing.Route(
                    classification,
                    childPath,
                    driverId,
                    context,
                    ct
                );

                if (outcome.Mode == "auto")
                {
                    await _routing.ExecuteAuto(outcome, context, ct);
                }
                else
                {
                    InboxItem item = outcome.Item;
                    context.InboxItems.Add(item);
                    await context.SaveChangesAsync(ct);

                    await _eventBus.PublishAsync(
                        new InboxItemDetectedEvent
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
                Logger.System(
                    $"InboxClassifier: Error processing {childPath}: {ex.Message}",
                    LogEventLevel.Error
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
                    failContext.InboxItems.Add(failedItem);
                    await failContext.SaveChangesAsync(ct);
                }
                catch (Exception saveEx)
                {
                    Logger.System(
                        $"InboxClassifier: Could not persist Failed item for {childPath}: {saveEx.Message}",
                        LogEventLevel.Error
                    );
                }

                await _eventBus.PublishAsync(
                    new InboxItemDetectedEvent
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
