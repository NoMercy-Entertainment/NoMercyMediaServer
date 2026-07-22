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

namespace NoMercy.Api.Services.Video;

public class VideoPlayerStateManager
{
    private readonly ConcurrentDictionary<Guid, VideoPlayerState> _playerStates = new();

    public IEnumerable<VideoPlayerState> GetAllStates()
    {
        return _playerStates.Values;
    }

    public VideoPlayerState? GetState(Guid userId)
    {
        return _playerStates.TryGetValue(key: userId, value: out VideoPlayerState? state) ? state : null;
    }

    public void UpdateState(Guid userId, VideoPlayerState state)
    {
        _playerStates.AddOrUpdate(key: userId, addValue: state, updateValueFactory: (_, _) => state);
    }

    public bool RemoveState(Guid userId)
    {
        return _playerStates.TryRemove(key: userId, value: out _);
    }

    public bool HasState(Guid userId)
    {
        return _playerStates.ContainsKey(key: userId);
    }

    public void ClearAllStates()
    {
        _playerStates.Clear();
    }

    public void UpdateStateProperty(Guid userId, Action<VideoPlayerState> updateAction)
    {
        if (_playerStates.TryGetValue(key: userId, value: out VideoPlayerState? state))
        {
            updateAction(obj: state);
            _playerStates[key: userId] = state;
        }
    }

    public bool TryGetValue(Guid userId, [NotNullWhen(returnValue: true)] out VideoPlayerState? state)
    {
        return _playerStates.TryGetValue(key: userId, value: out state);
    }
}
