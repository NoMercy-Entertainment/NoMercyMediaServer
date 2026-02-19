using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.EventHandlers;

public class FolderPathEventHandler : IDisposable
{
    private readonly IDbContextFactory<MediaContext> _contextFactory;
    private readonly List<IDisposable> _subscriptions = [];

    public FolderPathEventHandler(IEventBus eventBus, IDbContextFactory<MediaContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _subscriptions.Add(eventBus.Subscribe<FolderPathAddedEvent>(OnFolderPathAdded));
        _subscriptions.Add(eventBus.Subscribe<FolderPathRemovedEvent>(OnFolderPathRemoved));
    }

    internal Task OnFolderPathAdded(FolderPathAddedEvent @event, CancellationToken ct)
    {
        DynamicStaticFilesMiddleware.AddPath(@event.RequestPath, @event.PhysicalPath);

        using MediaContext mediaContext = _contextFactory.CreateDbContext();
        ClaimsPrincipleExtensions.RefreshFolderIds(mediaContext);

        return Task.CompletedTask;
    }

    internal Task OnFolderPathRemoved(FolderPathRemovedEvent @event, CancellationToken ct)
    {
        DynamicStaticFilesMiddleware.RemovePath(@event.RequestPath);

        using MediaContext mediaContext = _contextFactory.CreateDbContext();
        ClaimsPrincipleExtensions.RefreshFolderIds(mediaContext);

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
