using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace NoMercy.Api.Controllers.Socket.music;

public class PlayerStateManager
{
    private readonly ConcurrentDictionary<Guid, PlayerState> _playerStates = new();
    
    public IEnumerable<PlayerState> GetAllStates() =>
        _playerStates.Values;
    
    public PlayerState? GetState(Guid userId) =>
        _playerStates.TryGetValue(userId, out var state) ? state : null;

    public void UpdateState(Guid userId, PlayerState state) =>
        _playerStates.AddOrUpdate(userId, state, (_, _) => state);

    public bool RemoveState(Guid userId) =>
        _playerStates.TryRemove(userId, out _);
    
    public bool HasState(Guid userId) =>
        _playerStates.ContainsKey(userId);
    
    public void ClearAllStates() =>
        _playerStates.Clear();
    
    public void UpdateStateProperty(Guid userId, Action<PlayerState> updateAction)
    {
        if (_playerStates.TryGetValue(userId, out var state))
        {
            updateAction(state);
            _playerStates[userId] = state;
        }
    }
    
    public bool TryGetValue(Guid userId, [NotNullWhen(true)] out PlayerState? state) =>
        _playerStates.TryGetValue(userId, out state);
    
}