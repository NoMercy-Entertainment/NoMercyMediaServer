using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Api.EventHandlers;

public class SignalREncodingEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public SignalREncodingEventHandler(IEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<EncodingStartedEvent>(OnEncodingStarted));
        _subscriptions.Add(eventBus.Subscribe<EncodingProgressEvent>(OnEncodingProgress));
        _subscriptions.Add(eventBus.Subscribe<EncodingCompletedEvent>(OnEncodingCompleted));
        _subscriptions.Add(eventBus.Subscribe<EncodingFailedEvent>(OnEncodingFailed));
    }

    internal Task OnEncodingStarted(EncodingStartedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("EncodingStarted", "dashboardHub", new
        {
            @event.JobId,
            @event.InputPath,
            @event.OutputPath,
            @event.ProfileName,
            @event.Timestamp
        });

        Logger.Socket($"Encoding started: Job={@event.JobId}, Profile={@event.ProfileName}");
        return Task.CompletedTask;
    }

    internal Task OnEncodingProgress(EncodingProgressEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("EncodingProgress", "dashboardHub", new
        {
            @event.JobId,
            @event.Percentage,
            Elapsed = @event.Elapsed.TotalSeconds,
            Estimated = @event.Estimated?.TotalSeconds
        });

        return Task.CompletedTask;
    }

    internal Task OnEncodingCompleted(EncodingCompletedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("EncodingCompleted", "dashboardHub", new
        {
            @event.JobId,
            @event.OutputPath,
            Duration = @event.Duration.TotalSeconds,
            @event.Timestamp
        });

        Logger.Socket($"Encoding completed: Job={@event.JobId}");
        return Task.CompletedTask;
    }

    internal Task OnEncodingFailed(EncodingFailedEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("EncodingFailed", "dashboardHub", new
        {
            @event.JobId,
            @event.InputPath,
            @event.ErrorMessage,
            @event.ExceptionType,
            @event.Timestamp
        });

        Logger.Socket($"Encoding failed: Job={@event.JobId}, Error={@event.ErrorMessage}");
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
