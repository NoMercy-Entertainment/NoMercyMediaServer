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
using NoMercy.Api.Services.Music;
using NoMercy.Events;
using NoMercy.Events.Music;

namespace NoMercy.Api.EventHandlers;

public class MusicLikeEventHandler : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly MusicPlaybackService _musicPlaybackService;

    public MusicLikeEventHandler(IEventBus eventBus, MusicPlaybackService musicPlaybackService)
    {
        _musicPlaybackService = musicPlaybackService;
        _subscriptions.Add(item: eventBus.Subscribe<MusicItemLikedEvent>(handler: OnMusicItemLiked));
    }

    internal Task OnMusicItemLiked(MusicItemLikedEvent @event, CancellationToken ct)
    {
        return _musicPlaybackService.ApplyItemLikeAsync(
            userId: @event.UserId,
            itemId: @event.ItemId,
            liked: @event.Liked,
            cancellationToken: ct
        );
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
