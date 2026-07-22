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
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.EventHandlers;

public class SignalRLibraryScanEventHandler : IDisposable
{
    private readonly IClientMessenger _clientMessenger;
    private readonly List<IDisposable> _subscriptions = [];

    private readonly ILogger<SignalRLibraryScanEventHandler> _logger;

    public SignalRLibraryScanEventHandler(
        ILogger<SignalRLibraryScanEventHandler> logger,
        IEventBus eventBus,
        IClientMessenger clientMessenger
    )
    {
        _logger = logger;
        _clientMessenger = clientMessenger;
        _subscriptions.Add(item: eventBus.Subscribe<LibraryScanStartedEvent>(handler: OnScanStarted));
        _subscriptions.Add(item: eventBus.Subscribe<LibraryScanCompletedEvent>(handler: OnScanCompleted));
        _subscriptions.Add(item: eventBus.Subscribe<MediaAddedEvent>(handler: OnMediaAdded));
        _subscriptions.Add(item: eventBus.Subscribe<MediaRemovedEvent>(handler: OnMediaRemoved));
    }

    internal async Task OnScanStarted(LibraryScanStartedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            name: "LibraryScanStarted",
            endpoint: "dashboardHub",
            data: new
            {
                LibraryId = @event.LibraryId.ToString(),
                @event.LibraryName,
                @event.Timestamp,
            }
        );

        _logger.LogInformation(message: "Library scan started: {LibraryName}", args: @event.LibraryName);
    }

    internal async Task OnScanCompleted(LibraryScanCompletedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            name: "LibraryScanCompleted",
            endpoint: "dashboardHub",
            data: new
            {
                LibraryId = @event.LibraryId.ToString(),
                @event.LibraryName,
                @event.ItemsFound,
                Duration = @event.Duration.TotalSeconds,
                @event.Timestamp,
            }
        );

        _logger.LogInformation(
            message: "Library scan completed: {LibraryName}, {ItemsFound} items found", args: [@event.LibraryName, @event.ItemsFound]
        );
    }

    internal async Task OnMediaAdded(MediaAddedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            name: "MediaAdded",
            endpoint: "dashboardHub",
            data: new
            {
                @event.MediaId,
                @event.MediaType,
                @event.Title,
                LibraryId = @event.LibraryId.ToString(),
                @event.Timestamp,
            }
        );
    }

    internal async Task OnMediaRemoved(MediaRemovedEvent @event, CancellationToken ct)
    {
        await _clientMessenger.SendToAll(
            name: "MediaRemoved",
            endpoint: "dashboardHub",
            data: new
            {
                @event.MediaId,
                @event.MediaType,
                @event.Title,
                LibraryId = @event.LibraryId.ToString(),
                @event.Timestamp,
            }
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
