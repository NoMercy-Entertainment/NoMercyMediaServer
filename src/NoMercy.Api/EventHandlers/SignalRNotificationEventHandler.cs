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
using NoMercy.Events.Media;
using NoMercy.Networking.Dto;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.EventHandlers;

public class SignalRNotificationEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    public SignalRNotificationEventHandler(IEventBus eventBus, IClientMessenger clientMessenger)
    {
        _clientMessenger = clientMessenger;
        _subscriptions.Add(item: eventBus.Subscribe<UserNotifiedEvent>(handler: OnUserNotification));
    }

    internal async Task OnUserNotification(UserNotifiedEvent @event, CancellationToken ct)
    {
        NotifyDto payload = new()
        {
            Title = @event.Title,
            Message = @event.Message,
            Type = @event.Type,
        };

        if (@event.UserId is { } userId)
            await _clientMessenger.SendTo(name: "Notify", endpoint: @event.Hub, userId: userId, data: payload);
        else
            await _clientMessenger.SendToAll(name: "Notify", endpoint: @event.Hub, data: payload);
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
