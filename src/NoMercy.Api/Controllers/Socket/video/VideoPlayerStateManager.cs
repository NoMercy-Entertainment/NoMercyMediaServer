using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace NoMercy.Api.Controllers.Socket.video;

public class VideoPlayerStateManager
{
    private readonly ConcurrentDictionary<Guid, VideoPlayerState> _playerStates = new();

    public IEnumerable<VideoPlayerState> GetAllStates()
    {
        return _playerStates.Values;
    }

    public VideoPlayerState? GetState(Guid userId)
    {
        return _playerStates.TryGetValue(userId, out VideoPlayerState? state) ? state : null;
    }

    public void UpdateState(Guid userId, VideoPlayerState state)
    {
        _playerStates.AddOrUpdate(userId, state, (_, _) => state);
    }

    public bool RemoveState(Guid userId)
    {
        return _playerStates.TryRemove(userId, out _);
    }

    public bool HasState(Guid userId)
    {
        return _playerStates.ContainsKey(userId);
    }

    public void ClearAllStates()
    {
        _playerStates.Clear();
    }

    public void UpdateStateProperty(Guid userId, Action<VideoPlayerState> updateAction)
    {
        if (_playerStates.TryGetValue(userId, out VideoPlayerState? state))
        {
            updateAction(state);
            _playerStates[userId] = state;
        }
    }

    public bool TryGetValue(Guid userId, [NotNullWhen(true)] out VideoPlayerState? state)
    {
        return _playerStates.TryGetValue(userId, out state);
    }
}