// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace NoMercy.Api.Services.Music;

public class MusicPlayerStateManager
{
    private readonly ConcurrentDictionary<Guid, MusicPlayerState> _playerStates = new();

    public IEnumerable<MusicPlayerState> GetAllStates()
    {
        return _playerStates.Values;
    }

    public MusicPlayerState? GetState(Guid userId)
    {
        return _playerStates.TryGetValue(userId, out MusicPlayerState? state) ? state : null;
    }

    public void UpdateState(Guid userId, MusicPlayerState state)
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

    public void UpdateStateProperty(Guid userId, Action<MusicPlayerState> updateAction)
    {
        if (_playerStates.TryGetValue(userId, out MusicPlayerState? state))
        {
            updateAction(state);
            _playerStates[userId] = state;
        }
    }

    public bool TryGetValue(Guid userId, [NotNullWhen(true)] out MusicPlayerState? state)
    {
        return _playerStates.TryGetValue(userId, out state);
    }
}
