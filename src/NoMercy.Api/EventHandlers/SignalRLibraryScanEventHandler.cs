using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.EventHandlers;

public class SignalRLibraryScanEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    public SignalRLibraryScanEventHandler(IEventBus eventBus, IClientMessenger clientMessenger)
    {
        _clientMessenger = clientMessenger;
        _subscriptions.Add(eventBus.Subscribe<LibraryScanStartedEvent>(OnScanStarted));
        _subscriptions.Add(eventBus.Subscribe<LibraryScanCompletedEvent>(OnScanCompleted));
        _subscriptions.Add(eventBus.Subscribe<MediaAddedEvent>(OnMediaAdded));
        _subscriptions.Add(eventBus.Subscribe<MediaRemovedEvent>(OnMediaRemoved));
    }

    internal async Task OnScanStarted(LibraryScanStartedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "LibraryScanStarted",
            "dashboardHub",
            new
            {
                LibraryId = @event.LibraryId.ToString(),
                @event.LibraryName,
                @event.Timestamp,
            }
        );

        Logger.Socket($"Library scan started: {@event.LibraryName}");
    }

    internal async Task OnScanCompleted(LibraryScanCompletedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "LibraryScanCompleted",
            "dashboardHub",
            new
            {
                LibraryId = @event.LibraryId.ToString(),
                @event.LibraryName,
                @event.ItemsFound,
                Duration = @event.Duration.TotalSeconds,
                @event.Timestamp,
            }
        );

        Logger.Socket(
            $"Library scan completed: {@event.LibraryName}, {@event.ItemsFound} items found"
        );
    }

    internal async Task OnMediaAdded(MediaAddedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "MediaAdded",
            "dashboardHub",
            new
            {
                @event.MediaId,
                @event.MediaType,
                @event.Title,
                LibraryId = @event.LibraryId.ToString(),
                @event.Timestamp,
            }
        );
    }

    internal async Task OnMediaRemoved(MediaRemovedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "MediaRemoved",
            "dashboardHub",
            new
            {
                @event.MediaId,
                @event.MediaType,
                @event.Title,
                LibraryId = @event.LibraryId.ToString(),
                @event.Timestamp,
            }
        );
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
