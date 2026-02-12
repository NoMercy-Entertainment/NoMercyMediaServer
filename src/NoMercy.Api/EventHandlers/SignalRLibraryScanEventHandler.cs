using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.EventHandlers;

public class SignalRLibraryScanEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public SignalRLibraryScanEventHandler(IEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<LibraryScanStartedEvent>(OnScanStarted));
        _subscriptions.Add(eventBus.Subscribe<LibraryScanCompletedEvent>(OnScanCompleted));
        _subscriptions.Add(eventBus.Subscribe<MediaAddedEvent>(OnMediaAdded));
        _subscriptions.Add(eventBus.Subscribe<MediaRemovedEvent>(OnMediaRemoved));
    }

    internal Task OnScanStarted(LibraryScanStartedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("LibraryScanStarted", "dashboardHub", new
        {
            LibraryId = @event.LibraryId.ToString(),
            @event.LibraryName,
            @event.Timestamp
        });

        Logger.Socket($"Library scan started: {@event.LibraryName}");
        return Task.CompletedTask;
    }

    internal Task OnScanCompleted(LibraryScanCompletedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("LibraryScanCompleted", "dashboardHub", new
        {
            LibraryId = @event.LibraryId.ToString(),
            @event.LibraryName,
            @event.ItemsFound,
            Duration = @event.Duration.TotalSeconds,
            @event.Timestamp
        });

        Logger.Socket($"Library scan completed: {@event.LibraryName}, {@event.ItemsFound} items found");
        return Task.CompletedTask;
    }

    internal Task OnMediaAdded(MediaAddedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("MediaAdded", "dashboardHub", new
        {
            @event.MediaId,
            @event.MediaType,
            @event.Title,
            LibraryId = @event.LibraryId.ToString(),
            @event.Timestamp
        });

        return Task.CompletedTask;
    }

    internal Task OnMediaRemoved(MediaRemovedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("MediaRemoved", "dashboardHub", new
        {
            @event.MediaId,
            @event.MediaType,
            @event.Title,
            LibraryId = @event.LibraryId.ToString(),
            @event.Timestamp
        });

        return Task.CompletedTask;
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
