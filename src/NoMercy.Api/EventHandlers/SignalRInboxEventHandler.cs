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

using Microsoft.Extensions.Logging;
using NoMercy.Events;
using NoMercy.Events.Inbox;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.EventHandlers;

public class SignalRInboxEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    private readonly ILogger<SignalRInboxEventHandler> _logger;

    public SignalRInboxEventHandler(
        ILogger<SignalRInboxEventHandler> logger,
        IEventBus eventBus,
        IClientMessenger clientMessenger
    )
    {
        _logger = logger;
        _clientMessenger = clientMessenger;
        _subscriptions.Add(item: eventBus.Subscribe<InboxItemDetectedEvent>(handler: OnItemDetected));
        _subscriptions.Add(item: eventBus.Subscribe<InboxItemUpdatedEvent>(handler: OnItemUpdated));
    }

    internal async Task OnItemDetected(InboxItemDetectedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            name: "InboxItemAdded",
            endpoint: "dashboardHub",
            data: new
            {
                @event.Id,
                @event.DetectedType,
                @event.Confidence,
                @event.Status,
            }
        );

        _logger.LogInformation(
            message: "Inbox item detected: {Id} ({DetectedType}, {Confidence})", args: [@event.Id, @event.DetectedType, @event.Confidence]
        );
    }

    internal async Task OnItemUpdated(InboxItemUpdatedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            name: "InboxItemUpdated",
            endpoint: "dashboardHub",
            data: new { @event.Id, @event.Status }
        );

        _logger.LogInformation(message: "Inbox item updated: {Id} → {Status}", args: [@event.Id, @event.Status]);
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
