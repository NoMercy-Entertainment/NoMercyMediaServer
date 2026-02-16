using NoMercy.Events;
using NoMercy.Events.Media;
using NoMercy.Networking.Dto;

namespace NoMercy.Api.EventHandlers;

public class SignalRNotificationEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public SignalRNotificationEventHandler(IEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<UserNotificationEvent>(OnUserNotification));
    }

    internal Task OnUserNotification(UserNotificationEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("Notify", @event.Hub, new NotifyDto
        {
            Title = @event.Title,
            Message = @event.Message,
            Type = @event.Type
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
