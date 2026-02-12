using NoMercy.Events;
using NoMercy.Events.Playback;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.EventHandlers;

public class SignalRPlaybackEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public SignalRPlaybackEventHandler(IEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<PlaybackStartedEvent>(OnPlaybackStarted));
        _subscriptions.Add(eventBus.Subscribe<PlaybackProgressEvent>(OnPlaybackProgress));
        _subscriptions.Add(eventBus.Subscribe<PlaybackCompletedEvent>(OnPlaybackCompleted));
    }

    internal Task OnPlaybackStarted(PlaybackStartedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("PlaybackStarted", "dashboardHub", new
        {
            @event.UserId,
            @event.MediaId,
            @event.MediaIdentifier,
            @event.MediaType,
            @event.DeviceId,
            @event.Timestamp
        });

        Logger.Socket($"Playback started: User={@event.UserId}, Media={@event.MediaId}, Type={@event.MediaType}");
        return Task.CompletedTask;
    }

    internal Task OnPlaybackProgress(PlaybackProgressEvent @event, CancellationToken ct)
    {
        // Progress events are high-frequency; broadcast but don't log to avoid noise
        Networking.Networking.SendToAll("PlaybackProgress", "dashboardHub", new
        {
            @event.UserId,
            @event.MediaId,
            @event.MediaIdentifier,
            @event.Position,
            @event.Duration
        });

        return Task.CompletedTask;
    }

    internal Task OnPlaybackCompleted(PlaybackCompletedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("PlaybackCompleted", "dashboardHub", new
        {
            @event.UserId,
            @event.MediaId,
            @event.MediaIdentifier,
            @event.MediaType,
            @event.Timestamp
        });

        Logger.Socket($"Playback completed: User={@event.UserId}, Media={@event.MediaId}, Type={@event.MediaType}");
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
