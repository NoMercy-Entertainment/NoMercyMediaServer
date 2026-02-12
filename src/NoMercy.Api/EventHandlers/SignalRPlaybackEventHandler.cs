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
        Logger.Socket($"Playback started: User={@event.UserId}, Media={@event.MediaId}, Type={@event.MediaType}");
        return Task.CompletedTask;
    }

    internal Task OnPlaybackProgress(PlaybackProgressEvent @event, CancellationToken ct)
    {
        // Progress events are high-frequency; only log at debug level to avoid noise
        return Task.CompletedTask;
    }

    internal Task OnPlaybackCompleted(PlaybackCompletedEvent @event, CancellationToken ct)
    {
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
