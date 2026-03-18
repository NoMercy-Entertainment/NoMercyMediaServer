using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Events;
using NoMercy.Events.Music;

namespace NoMercy.Api.EventHandlers;

public class MusicLikeEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly MusicPlayerStateManager _musicPlayerStateManager;
    private readonly MusicPlaybackService _musicPlaybackService;
    private readonly IServiceScopeFactory _scopeFactory;

    public MusicLikeEventHandler(
        IEventBus eventBus,
        MusicPlayerStateManager musicPlayerStateManager,
        MusicPlaybackService musicPlaybackService,
        IServiceScopeFactory scopeFactory)
    {
        _musicPlayerStateManager = musicPlayerStateManager;
        _musicPlaybackService = musicPlaybackService;
        _scopeFactory = scopeFactory;

        _subscriptions.Add(eventBus.Subscribe<MusicItemLikedEvent>(OnMusicItemLiked));
    }

    internal async Task OnMusicItemLiked(MusicItemLikedEvent @event, CancellationToken ct)
    {
        if (!_musicPlayerStateManager.TryGetValue(@event.UserId, out MusicPlayerState? playerState))
            return;

        if (playerState.CurrentItem != null && playerState.CurrentItem.Id == @event.ItemId)
            playerState.CurrentItem.Favorite = @event.Liked;

        foreach (PlaylistTrackDto track in playerState.Playlist)
            if (track.Id == @event.ItemId)
                track.Favorite = @event.Liked;

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IDbContextFactory<MediaContext> contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MediaContext>>();
        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        User? user = await context.Users.FirstOrDefaultAsync(u => u.Id == @event.UserId, ct);
        if (user is null) return;

        await _musicPlaybackService.UpdatePlaybackState(user, playerState);
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
