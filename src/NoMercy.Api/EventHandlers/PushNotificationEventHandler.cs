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
using NoMercy.Events.Library;
using NoMercy.NmSystem.Auth;
using NoMercy.Notifications.Push;

namespace NoMercy.Api.EventHandlers;

// A second, independent sink beside the SignalR*EventHandler classes: it
// subscribes to the same real server events they already broadcast on, and
// hands each one to NotificationSink under its channel slug. It never touches
// IClientMessenger, so a push failure cannot affect the live SignalR path —
// IEventBus.PublishAsync (InMemoryEventBus) invokes every subscriber of an
// event independently and catches per-subscriber, so one throwing subscriber
// never stops another. PushDispatcher already swallows its own failures, so
// this class has nothing left to guard beyond a missing access token.
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
        _subscriptions.Add(eventBus.Subscribe<LibraryRefreshedEvent>(OnLibraryRefreshed));
    }

    internal Task OnEncodingCompleted(EncodingCompletedEvent @event, CancellationToken ct) =>
        NotifyAsync(
            "encode-finished",
            new(
                "Encoding finished",
                $"{Path.GetFileName(@event.OutputPath)} finished encoding",
                null
            ),
            ct
        );

    internal Task OnEncodingFailed(EncodingFailedEvent @event, CancellationToken ct) =>
        NotifyAsync("encode-failed", new("Encoding failed", @event.ErrorMessage, null), ct);

    internal Task OnLibraryRefreshed(LibraryRefreshedEvent @event, CancellationToken ct) =>
        NotifyAsync(
            "library-updated",
            new("Library updated", "Your library was refreshed", null),
            ct
        );

    private Task NotifyAsync(string channel, PushPayload payload, CancellationToken ct)
    {
        string? accessToken = _authTokenStore.AccessToken;
        return accessToken is null
            ? Task.CompletedTask
            : _notificationSink.NotifyAsync(channel, payload, accessToken, ct);
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
