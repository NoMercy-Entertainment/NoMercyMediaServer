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

namespace NoMercy.Events;

public class LoggingEventBusDecorator : IEventBus
{
    private readonly IEventBus _inner;
    private readonly Action<string> _log;
    private readonly HashSet<string> _excluded;

    public LoggingEventBusDecorator(
        IEventBus inner,
        Action<string> log,
        IEnumerable<string>? excludedEventTypes = null
    )
    {
        _inner = inner ?? throw new ArgumentNullException(paramName: nameof(inner));
        _log = log ?? throw new ArgumentNullException(paramName: nameof(log));
        _excluded = new(collection: excludedEventTypes ?? [], comparer: StringComparer.Ordinal);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent
    {
        string eventTypeName = typeof(TEvent).Name;
        if (!_excluded.Contains(item: eventTypeName))
        {
            _log(
                obj: $"[Event] {eventTypeName} | Source={@event.Source} | EventId={@event.EventId} | Timestamp={@event.Timestamp:O}"
            );
        }

        await _inner.PublishAsync(@event: @event, ct: ct);
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IEvent
    {
        return _inner.Subscribe(handler: handler);
    }

    public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        return _inner.Subscribe(handler: handler);
    }
}
