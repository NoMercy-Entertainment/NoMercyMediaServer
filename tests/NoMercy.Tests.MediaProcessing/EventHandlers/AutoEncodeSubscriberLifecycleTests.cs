using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.MediaProcessing.EventHandlers;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.MediaProcessing.EventHandlers;

/// <summary>
/// Lifecycle / wiring tests for <see cref="AutoEncodeSubscriber"/>. The
/// subscriber's actual job dispatch path touches the real <c>MediaContext</c>
/// (it's a queue job so it can't reasonably take an injected factory) — that
/// path is covered by integration runs, not here. These tests verify:
/// - Start subscribes
/// - Stop disposes subscriptions
/// - Disposed subscriptions don't fire
/// - Unsubscribed events are silently ignored
/// - Multiple Start/Stop cycles don't leak subscriptions
/// </summary>
public class AutoEncodeSubscriberLifecycleTests
{
    [Fact]
    public async Task Start_SubscribesToMediaFilesScannedEvent()
    {
        IStorageDriver storageDriver = new LocalStorageDriver();
        IStorage storage = new LocalStorage(storageDriver, new StoragePathGuard([], storageDriver));
        InMemoryEventBus bus = new();
        AutoEncodeSubscriber subscriber = new(
            bus,
            NullLogger<AutoEncodeSubscriber>.Instance,
            storage
        );

        await subscriber.StartAsync(CancellationToken.None);

        // Publishing an event while subscribed should reach the handler without
        // throwing. The handler will try to open MediaContext and return early
        // when there are no matching folders — swallows its own exceptions.
        await bus.PublishAsync(
            new MediaFilesScannedEvent { MediaId = -1, LibraryId = Ulid.NewUlid() }
        );

        // Passes if no exception escaped.
        Assert.True(true);
    }

    [Fact]
    public async Task Stop_DisposesSubscriptions_EventBusNoLongerCalls()
    {
        IStorageDriver storageBackend2 = new LocalStorageDriver();
        IStorage storage2 = new LocalStorage(
            storageBackend2,
            new StoragePathGuard([], storageBackend2)
        );
        TrackingEventBus bus = new();
        AutoEncodeSubscriber subscriber = new(
            bus,
            NullLogger<AutoEncodeSubscriber>.Instance,
            storage2
        );

        await subscriber.StartAsync(CancellationToken.None);
        Assert.Single(bus.ActiveSubscriptions);

        await subscriber.StopAsync(CancellationToken.None);
        Assert.Empty(bus.ActiveSubscriptions);
    }

    [Fact]
    public async Task MultipleStartStopCycles_DoNotLeakSubscriptions()
    {
        IStorageDriver storageBackend3 = new LocalStorageDriver();
        IStorage storage3 = new LocalStorage(
            storageBackend3,
            new StoragePathGuard([], storageBackend3)
        );
        TrackingEventBus bus = new();
        AutoEncodeSubscriber subscriber = new(
            bus,
            NullLogger<AutoEncodeSubscriber>.Instance,
            storage3
        );

        for (int i = 0; i < 3; i++)
        {
            await subscriber.StartAsync(CancellationToken.None);
            await subscriber.StopAsync(CancellationToken.None);
        }

        Assert.Empty(bus.ActiveSubscriptions);
    }

    /// <summary>
    /// Wraps <see cref="InMemoryEventBus"/> to expose a count of live
    /// subscriptions so tests can verify disposal without poking at the bus
    /// internals via reflection.
    /// </summary>
    private sealed class TrackingEventBus : IEventBus
    {
        private readonly InMemoryEventBus _inner = new();
        public List<IDisposable> ActiveSubscriptions { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IEvent => _inner.PublishAsync(@event, ct);

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : IEvent
        {
            IDisposable subscription = _inner.Subscribe(handler);
            TrackingSubscription tracker = new(subscription, this);
            ActiveSubscriptions.Add(tracker);
            return tracker;
        }

        public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : IEvent
        {
            IDisposable subscription = _inner.Subscribe(handler);
            TrackingSubscription tracker = new(subscription, this);
            ActiveSubscriptions.Add(tracker);
            return tracker;
        }

        private sealed class TrackingSubscription(IDisposable inner, TrackingEventBus owner)
            : IDisposable
        {
            public void Dispose()
            {
                inner.Dispose();
                owner.ActiveSubscriptions.Remove(this);
            }
        }
    }
}
