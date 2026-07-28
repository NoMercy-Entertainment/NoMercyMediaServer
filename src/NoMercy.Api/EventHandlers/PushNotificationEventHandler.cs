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
using NoMercy.NmSystem.Auth;
using NoMercy.Notifications.Push;

namespace NoMercy.Api.EventHandlers;

/// <summary>
/// A second, independent sink beside the SignalR*EventHandler classes. It
/// never touches IClientMessenger, and it hands off to a queue rather than
/// awaiting the relay, so neither a push failure nor an unreachable
/// nomercy.tv can affect the live SignalR path or the publisher.
///
/// Only events that describe something a person asked to be told about belong
/// here. LibraryRefreshedEvent does not: it is a cache-invalidation signal
/// carrying a QueryKey, published from dozens of sites and several times per
/// single user action, including every continue-watching edit.
/// </summary>
public class PushNotificationEventHandler : IDisposable
{
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
    }

    internal Task OnEncodingCompleted(EncodingCompletedEvent @event, CancellationToken _)
    {
        Notify(
            "encode-finished",
            new(
                "Encoding finished",
                $"{Path.GetFileName(@event.OutputPath)} finished encoding",
                null
            )
        );
        return Task.CompletedTask;
    }

    internal Task OnEncodingFailed(EncodingFailedEvent @event, CancellationToken _)
    {
        Notify("encode-failed", new("Encoding failed", @event.ErrorMessage, null));
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
