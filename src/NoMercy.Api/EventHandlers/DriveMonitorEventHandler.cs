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
using NoMercy.Events.DriveMonitor;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.EventHandlers;

public class DriveMonitorEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    public DriveMonitorEventHandler(IEventBus eventBus, IClientMessenger clientMessenger)
    {
        _clientMessenger = clientMessenger;
        _subscriptions.Add(item: eventBus.Subscribe<DriveStateChangedEvent>(handler: OnDriveStateChanged));
    }

    internal async Task OnDriveStateChanged(DriveStateChangedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(name: "DriveState", endpoint: "ripperHub", data: @event.DriveStateData);
        await _clientMessenger.SendToAll(name: "DriveState", endpoint: "drivesHub", data: @event.DriveStateData);
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
