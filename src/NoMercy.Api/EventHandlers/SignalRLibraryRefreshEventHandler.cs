using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Networking.Dto;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.EventHandlers;

public class SignalRLibraryRefreshEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    public SignalRLibraryRefreshEventHandler(IEventBus eventBus, IClientMessenger clientMessenger)
    {
        _clientMessenger = clientMessenger;
        _subscriptions.Add(eventBus.Subscribe<LibraryRefreshEvent>(OnLibraryRefresh));
    }

    internal Task OnLibraryRefresh(LibraryRefreshEvent @event, CancellationToken ct)
    {
        _clientMessenger.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
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
