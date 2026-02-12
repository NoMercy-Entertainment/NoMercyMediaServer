using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Networking.Dto;

namespace NoMercy.Api.EventHandlers;

public class SignalRLibraryRefreshEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public SignalRLibraryRefreshEventHandler(IEventBus eventBus)
    {
        _subscriptions.Add(eventBus.Subscribe<LibraryRefreshEvent>(OnLibraryRefresh));
    }

    internal Task OnLibraryRefresh(LibraryRefreshEvent @event, CancellationToken ct)
    {
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = @event.QueryKey
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
