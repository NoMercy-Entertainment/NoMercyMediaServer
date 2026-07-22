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
using System.Text.Json;

namespace NoMercy.Events.Audit;

public sealed class EventAuditLog
{
    private readonly ConcurrentQueue<EventAuditEntry> _entries = new();
    private readonly EventAuditOptions _options;
    private int _count;

    public EventAuditLog(EventAuditOptions? options = null)
    {
        _options = options ?? new EventAuditOptions();
    }

    public bool Enabled => _options.Enabled;
    public int Count => _count;

    public void Record(IEvent @event, string eventTypeName)
    {
        if (!_options.Enabled)
            return;
        if (_options.ExcludedEventTypes.Contains(item: eventTypeName))
            return;

        EventAuditEntry entry = new()
        {
            EventId = @event.EventId,
            EventType = eventTypeName,
            Source = @event.Source,
            Timestamp = @event.Timestamp,
            Payload = SerializePayload(@event: @event),
        };

        _entries.Enqueue(item: entry);
        Interlocked.Increment(location: ref _count);

        if (_count > _options.MaxEntries)
            Compact();
    }

    public IReadOnlyList<EventAuditEntry> GetEntries()
    {
        return _entries.ToArray();
    }

    public IReadOnlyList<EventAuditEntry> GetEntries(string eventType)
    {
        return _entries.Where(predicate: entry => entry.EventType == eventType).ToArray();
    }

    public IReadOnlyList<EventAuditEntry> GetEntries(DateTime from, DateTime to)
    {
        return _entries.Where(predicate: entry => entry.Timestamp >= from && entry.Timestamp <= to).ToArray();
    }

    public void Clear()
    {
        while (_entries.TryDequeue(result: out _)) { }
        Interlocked.Exchange(location1: ref _count, value: 0);
    }

    private void Compact()
    {
        int toRemove = (int)(_options.MaxEntries * _options.CompactionPercentage);
        for (int i = 0; i < toRemove; i++)
        {
            if (_entries.TryDequeue(result: out _))
                Interlocked.Decrement(location: ref _count);
        }
    }

    private static string SerializePayload(IEvent @event)
    {
        try
        {
            return JsonSerializer.Serialize(
                value: @event,
                inputType: @event.GetType(),
                options: new JsonSerializerOptions { WriteIndented = false }
            );
        }
        catch
        {
            return $"{{\"EventId\":\"{@event.EventId}\"}}";
        }
    }
}
