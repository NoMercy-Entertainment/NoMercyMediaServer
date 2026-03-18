using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Api.Middleware;
using NoMercy.Database;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Helpers.Extensions;

namespace NoMercy.Api.EventHandlers;

public class FolderPathEventHandler : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly List<IDisposable> _subscriptions = [];

    public FolderPathEventHandler(IEventBus eventBus, IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _subscriptions.Add(eventBus.Subscribe<FolderPathAddedEvent>(OnFolderPathAdded));
        _subscriptions.Add(eventBus.Subscribe<FolderPathRemovedEvent>(OnFolderPathRemoved));
    }

    internal Task OnFolderPathAdded(FolderPathAddedEvent @event, CancellationToken ct)
    {
        DynamicStaticFilesMiddleware.AddPath(@event.RequestPath, @event.PhysicalPath);

        using IServiceScope scope = _scopeFactory.CreateScope();
        IDbContextFactory<MediaContext> contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MediaContext>>();
        using MediaContext mediaContext = contextFactory.CreateDbContext();
        ClaimsPrincipleExtensions.RefreshFolderIds(mediaContext);

        return Task.CompletedTask;
    }

    internal Task OnFolderPathRemoved(FolderPathRemovedEvent @event, CancellationToken ct)
    {
        DynamicStaticFilesMiddleware.RemovePath(@event.RequestPath);

        using IServiceScope scope = _scopeFactory.CreateScope();
        IDbContextFactory<MediaContext> contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MediaContext>>();
        using MediaContext mediaContext = contextFactory.CreateDbContext();
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
