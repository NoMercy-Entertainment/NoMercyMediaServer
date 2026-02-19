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
        if (!_options.Enabled) return;
        if (_options.ExcludedEventTypes.Contains(eventTypeName)) return;

        EventAuditEntry entry = new()
        {
            EventId = @event.EventId,
            EventType = eventTypeName,
            Source = @event.Source,
            Timestamp = @event.Timestamp,
            Payload = SerializePayload(@event)
        };

        _entries.Enqueue(entry);
        Interlocked.Increment(ref _count);

        if (_count > _options.MaxEntries)
            Compact();
    }

    public IReadOnlyList<EventAuditEntry> GetEntries()
    {
        return _entries.ToArray();
    }

    public IReadOnlyList<EventAuditEntry> GetEntries(string eventType)
    {
        return _entries.Where(e => e.EventType == eventType).ToArray();
    }

    public IReadOnlyList<EventAuditEntry> GetEntries(DateTime from, DateTime to)
    {
        return _entries.Where(e => e.Timestamp >= from && e.Timestamp <= to).ToArray();
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _count, 0);
    }

    private void Compact()
    {
        int toRemove = (int)(_options.MaxEntries * _options.CompactionPercentage);
        for (int i = 0; i < toRemove; i++)
        {
            if (_entries.TryDequeue(out _))
                Interlocked.Decrement(ref _count);
        }
    }

    private static string SerializePayload(IEvent @event)
    {
        try
        {
            return JsonSerializer.Serialize(@event, @event.GetType(), new JsonSerializerOptions
            {
                WriteIndented = false
            });
        }
        catch
        {
            return $"{{\"EventId\":\"{@event.EventId}\"}}";
        }
    }
}
