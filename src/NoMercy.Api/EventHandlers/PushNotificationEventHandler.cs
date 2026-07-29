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

using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.Events.Media;
using NoMercy.NmSystem.Auth;
using NoMercy.Notifications.Push;

namespace NoMercy.Api.EventHandlers;

/// <summary>
/// Only events that describe something a person asked to be told about belong
/// here. LibraryRefreshedEvent does not: it is a cache-invalidation signal
/// carrying a QueryKey, published from dozens of sites and several times per
/// single user action, including every continue-watching edit.
/// </summary>
public class PushNotificationEventHandler : IDisposable
{
    private const string UserNotificationChannel = "user-notification";

    private readonly IAuthTokenStore _authTokenStore;
    private readonly NotificationSink _notificationSink;
    private readonly List<IDisposable> _subscriptions = [];

    public PushNotificationEventHandler(
        IEventBus eventBus,
        IAuthTokenStore authTokenStore,
        NotificationSink notificationSink
    )
    {
        _authTokenStore = authTokenStore;
        _notificationSink = notificationSink;
        _subscriptions.Add(eventBus.Subscribe<EncodingCompletedEvent>(OnEncodingCompleted));
        _subscriptions.Add(eventBus.Subscribe<EncodingFailedEvent>(OnEncodingFailed));
        _subscriptions.Add(eventBus.Subscribe<UserNotifiedEvent>(OnUserNotified));
    }

    internal Task OnEncodingCompleted(EncodingCompletedEvent @event, CancellationToken _)
    {
        Notify(
            "encode-finished",
            new(
                "Encoding finished",
                $"{Path.GetFileName(@event.OutputPath)} finished encoding",
                null,
                Image: PushArtworkUrl.Build(@event.BackdropPath, PushArtworkUrl.BackdropWidth),
                Icon: PushArtworkUrl.Build(@event.PosterPath, PushArtworkUrl.PosterWidth)
            )
        );
        return Task.CompletedTask;
    }

    internal Task OnEncodingFailed(EncodingFailedEvent @event, CancellationToken _)
    {
        Notify(
            "encode-failed",
            new(
                "Encoding failed",
                @event.ErrorMessage,
                null,
                Image: PushArtworkUrl.Build(@event.BackdropPath, PushArtworkUrl.BackdropWidth),
                Icon: PushArtworkUrl.Build(@event.PosterPath, PushArtworkUrl.PosterWidth)
            )
        );
        return Task.CompletedTask;
    }

    // The access token gates push, not the whole notification: an unregistered
    // server still has to reach its own users over SignalR.
    internal Task OnUserNotified(UserNotifiedEvent @event, CancellationToken _)
    {
        if (@event.UserId is not { } userId)
            return Task.CompletedTask;

        _notificationSink.NotifyUser(
            userId,
            @event.Hub,
            UserNotificationChannel,
            new(
                @event.Title,
                @event.Message,
                @event.Route,
                @event.Type,
                Image: PushArtworkUrl.Build(@event.BackdropPath, PushArtworkUrl.BackdropWidth),
                Icon: PushArtworkUrl.Build(@event.PosterPath, PushArtworkUrl.PosterWidth)
            ),
            _authTokenStore.AccessToken ?? string.Empty
        );

        return Task.CompletedTask;
    }

    private void Notify(string channel, PushPayload payload)
    {
        string? accessToken = _authTokenStore.AccessToken;
        if (accessToken is null)
            return;

        _notificationSink.Notify(channel, payload, accessToken);
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
