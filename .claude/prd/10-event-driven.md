## 10. Event-Driven Architecture

### 10.1 Overview

Transition from tightly-coupled direct method calls to an event-driven system where components communicate through events. This enables:
- Plugin system integration (plugins subscribe to events)
- Loose coupling between components
- Easier testing (mock event bus)
- Better scalability (async event processing)
- Activity logging/auditing for free

### 10.2 Core Event Infrastructure

#### Event Bus Interface
```csharp
// src/NoMercy.Events/IEventBus.cs
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent;

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IEvent;

    IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent;
}

public interface IEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
    string Source { get; }
}

public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
```

#### In-Process Implementation (Phase 1)
```csharp
public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct)
        where TEvent : IEvent
    {
        if (_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            foreach (var handler in handlers.ToArray())
            {
                await ((Func<TEvent, CancellationToken, Task>)handler)(@event, ct);
            }
        }
    }
}
```

### 10.3 Domain Events

| Event | Trigger | Consumers |
|-------|---------|-----------|
| `MediaDiscoveredEvent` | New file found during scan | Metadata fetcher, thumbnail generator, plugins |
| `MediaAddedEvent` | Media fully added to library | UI notification, search indexer, plugins |
| `MediaRemovedEvent` | Media removed from library | Cleanup, search indexer, plugins |
| `EncodingStartedEvent` | Encoding job begins | Dashboard, progress tracker, plugins |
| `EncodingProgressEvent` | Encoding progress update | Dashboard (rate-limited) |
| `EncodingCompletedEvent` | Encoding finished | HLS playlist generator, notification, plugins |
| `EncodingFailedEvent` | Encoding error | Retry logic, notification, cleanup |
| `UserAuthenticatedEvent` | User login | Session tracker, analytics, plugins |
| `UserDisconnectedEvent` | SignalR disconnect | State cleanup, analytics |
| `PlaybackStartedEvent` | Media playback begins | Scrobbling, analytics, plugins |
| `PlaybackProgressEvent` | Watch progress update | Resume tracking, plugins |
| `PlaybackCompletedEvent` | Media fully watched | Watch history, recommendations, plugins |
| `LibraryScanStartedEvent` | Library scan triggered | Dashboard notification |
| `LibraryScanCompletedEvent` | Library scan done | Dashboard, index refresh |
| `PluginLoadedEvent` | Plugin activated | Dashboard, logging |
| `PluginErrorEvent` | Plugin failure | Error logging, dashboard |
| `ConfigurationChangedEvent` | Settings modified | All dependent services |

### 10.4 Migration Strategy

**Phase 1**: Add `IEventBus` alongside existing code (no breaking changes)
**Phase 2**: Replace direct calls with event publishing
**Phase 3**: Move consumers to event handlers
**Phase 4**: Allow plugins to subscribe

### 10.5 Implementation Tasks

| Task ID | Description | Effort |
|---------|-------------|--------|
| EVT-01 | Create `NoMercy.Events` project with IEventBus, IEvent, IEventHandler | Small |
| EVT-02 | Implement `InMemoryEventBus` with ConcurrentDictionary | Small |
| EVT-03 | Register event bus as singleton in DI | Small |
| EVT-04 | Define all domain event classes | Medium |
| EVT-05 | Add event publishing to media scan pipeline | Medium |
| EVT-06 | Add event publishing to encoding pipeline | Medium |
| EVT-07 | Add event publishing to playback services | Medium |
| EVT-08 | Add event publishing to user authentication | Small |
| EVT-09 | Migrate SignalR broadcasting to event handlers | Medium |
| EVT-10 | Add event logging middleware (audit trail) | Small |
| EVT-11 | Create plugin event subscription API | Medium |

---

