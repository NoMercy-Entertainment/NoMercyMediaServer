using NoMercy.Events;
using NoMercy.Events.Inbox;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.EventHandlers;

public class SignalRInboxEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    public SignalRInboxEventHandler(IEventBus eventBus, IClientMessenger clientMessenger)
    {
        _clientMessenger = clientMessenger;
        _subscriptions.Add(eventBus.Subscribe<InboxItemDetectedEvent>(OnItemDetected));
        _subscriptions.Add(eventBus.Subscribe<InboxItemUpdatedEvent>(OnItemUpdated));
    }

    internal async Task OnItemDetected(InboxItemDetectedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "InboxItemAdded",
            "dashboardHub",
            new
            {
                @event.Id,
                @event.DetectedType,
                @event.Confidence,
                @event.Status,
            }
        );

        Logger.Socket(
            $"Inbox item detected: {@event.Id} ({@event.DetectedType}, {@event.Confidence})"
        );
    }

    internal async Task OnItemUpdated(InboxItemUpdatedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            "InboxItemUpdated",
            "dashboardHub",
            new { @event.Id, @event.Status }
        );

        Logger.Socket($"Inbox item updated: {@event.Id} → {@event.Status}");
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
