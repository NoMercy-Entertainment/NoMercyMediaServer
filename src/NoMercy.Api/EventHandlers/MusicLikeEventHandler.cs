using Microsoft.Extensions.DependencyInjection;
using NoMercy.Api.DTOs.Music;
using NoMercy.Api.Services.Music;
using NoMercy.Data.Repositories;
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
        IServiceScopeFactory scopeFactory
    )
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
        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();
        User? user = await userRepository.GetByIdAsync(@event.UserId);
        if (user is null)
            return;

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
