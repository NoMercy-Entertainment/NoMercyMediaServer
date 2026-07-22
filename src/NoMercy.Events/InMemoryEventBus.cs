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
using Microsoft.Extensions.Logging;

namespace NoMercy.Events;

public class InMemoryEventBus(ILogger<InMemoryEventBus>? logger = null) : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IEvent
    {
        if (!_handlers.TryGetValue(key: typeof(TEvent), value: out List<Delegate>? handlers))
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
            try
            {
                await ((Func<TEvent, CancellationToken, Task>)handler)(arg1: @event, arg2: ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    exception: ex,
                    message: "Event handler for {EventType} failed — other handlers will still execute",
                    args: typeof(TEvent).Name
                );
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IEvent
    {
        List<Delegate> handlers = _handlers.GetOrAdd(key: typeof(TEvent), valueFactory: _ => []);

        lock (_lock)
        {
            handlers.Add(item: handler);
        }

        return new Subscription(onDispose: () =>
        {
            lock (_lock)
            {
                handlers.Remove(item: handler);
            }
        });
    }

    public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        Func<TEvent, CancellationToken, Task> wrapper = handler.HandleAsync;
        return Subscribe(handler: wrapper);
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(location1: ref _disposed, value: 1) == 0)
            {
                onDispose();
            }
        }
    }
}
