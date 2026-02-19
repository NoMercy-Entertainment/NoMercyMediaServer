using System.Collections.Concurrent;

namespace NoMercy.Events;

public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out List<Delegate>? handlers))
        {
            return;
        }

        Delegate[] snapshot;
        lock (_lock)
        {
            snapshot = handlers.ToArray();
        }

        foreach (Delegate handler in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            await ((Func<TEvent, CancellationToken, Task>)handler)(@event, ct);
        }
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IEvent
    {
        List<Delegate> handlers = _handlers.GetOrAdd(typeof(TEvent), _ => []);

        lock (_lock)
        {
            handlers.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_lock)
            {
                handlers.Remove(handler);
            }
        });
    }

    public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        Func<TEvent, CancellationToken, Task> wrapper = handler.HandleAsync;
        return Subscribe(wrapper);
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                onDispose();
            }
        }
    }
}
