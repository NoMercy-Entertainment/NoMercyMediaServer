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

using FluentAssertions;
using NoMercy.Events;
using Xunit;

namespace NoMercy.Tests.Events;

public class InMemoryEventBusTests
{
    private sealed class TestEvent : EventBase
    {
        public override string Source => "Test";
        public string Data { get; init; } = string.Empty;
    }

    private sealed class OtherEvent : EventBase
    {
        public override string Source => "Other";
    }

    private sealed class TestHandler : IEventHandler<TestEvent>
    {
        public List<TestEvent> Received { get; } = [];

        public Task HandleAsync(TestEvent @event, CancellationToken ct = default)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_DoesNotThrow()
    {
        InMemoryEventBus bus = new();
        TestEvent evt = new() { Data = "hello" };

        Func<Task> act = () => bus.PublishAsync(evt);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithDelegateSubscriber_InvokesHandler()
    {
        InMemoryEventBus bus = new();
        List<TestEvent> received = [];

        bus.Subscribe<TestEvent>(
            (e, _) =>
            {
                received.Add(e);
                return Task.CompletedTask;
            }
        );

        TestEvent evt = new() { Data = "test-data" };
        await bus.PublishAsync(evt);

        received.Should().ContainSingle().Which.Data.Should().Be("test-data");
    }

    [Fact]
    public async Task PublishAsync_WithEventHandlerSubscriber_InvokesHandler()
    {
        InMemoryEventBus bus = new();
        TestHandler handler = new();

        bus.Subscribe(handler);

        TestEvent evt = new() { Data = "handler-test" };
        await bus.PublishAsync(evt);

        handler.Received.Should().ContainSingle().Which.Data.Should().Be("handler-test");
    }

    [Fact]
    public async Task PublishAsync_MultipleSubscribers_InvokesAll()
    {
        InMemoryEventBus bus = new();
        List<string> order = [];

        bus.Subscribe<TestEvent>(
            (_, _) =>
            {
                order.Add("first");
                return Task.CompletedTask;
            }
        );

        bus.Subscribe<TestEvent>(
            (_, _) =>
            {
                order.Add("second");
                return Task.CompletedTask;
            }
        );

        TestHandler handler = new();
        bus.Subscribe(handler);

        await bus.PublishAsync(new TestEvent { Data = "multi" });

        order.Should().Equal(["first", "second"]);
        handler.Received.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishAsync_DifferentEventTypes_OnlyMatchingHandlersCalled()
    {
        InMemoryEventBus bus = new();
        List<TestEvent> testReceived = [];
        List<OtherEvent> otherReceived = [];

        bus.Subscribe<TestEvent>(
            (e, _) =>
            {
                testReceived.Add(e);
                return Task.CompletedTask;
            }
        );

        bus.Subscribe<OtherEvent>(
            (e, _) =>
            {
                otherReceived.Add(e);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(new TestEvent { Data = "for-test" });

        testReceived.Should().ContainSingle();
        otherReceived.Should().BeEmpty();
    }

    [Fact]
    public async Task Subscribe_ReturnsDisposable_UnsubscribesOnDispose()
    {
        InMemoryEventBus bus = new();
        List<TestEvent> received = [];

        IDisposable subscription = bus.Subscribe<TestEvent>(
            (e, _) =>
            {
                received.Add(e);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(new TestEvent { Data = "before" });
        received.Should().ContainSingle();

        subscription.Dispose();

        await bus.PublishAsync(new TestEvent { Data = "after" });
        received.Should().ContainSingle("handler should not be called after dispose");
    }

    [Fact]
    public void Subscribe_DisposeCalledTwice_DoesNotThrow()
    {
        InMemoryEventBus bus = new();

        IDisposable subscription = bus.Subscribe<TestEvent>((_, _) => Task.CompletedTask);

        Action act = () =>
        {
            subscription.Dispose();
            subscription.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task PublishAsync_CancellationRequested_StopsProcessing()
    {
        InMemoryEventBus bus = new();
        CancellationTokenSource cts = new();
        List<string> order = [];

        bus.Subscribe<TestEvent>(
            (_, _) =>
            {
                order.Add("first");
                cts.Cancel();
                return Task.CompletedTask;
            }
        );

        bus.Subscribe<TestEvent>(
            (_, _) =>
            {
                order.Add("second");
                return Task.CompletedTask;
            }
        );

        Func<Task> act = () => bus.PublishAsync(new TestEvent(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        order.Should().Equal("first");
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_IsIsolatedAndOtherHandlersStillRun()
    {
        InMemoryEventBus bus = new();
        List<string> ran = [];

        bus.Subscribe<TestEvent>((_, _) => throw new InvalidOperationException("handler error"));
        bus.Subscribe<TestEvent>(
            (_, _) =>
            {
                ran.Add("second");
                return Task.CompletedTask;
            }
        );

        Func<Task> act = () => bus.PublishAsync(new TestEvent());

        await act.Should().NotThrowAsync();
        ran.Should().Equal("second");
    }

    [Fact]
    public async Task PublishAsync_EventHandlerDisposed_NoLongerCalled()
    {
        InMemoryEventBus bus = new();
        TestHandler handler = new();

        IDisposable sub = bus.Subscribe(handler);
        await bus.PublishAsync(new TestEvent { Data = "before" });
        handler.Received.Should().HaveCount(1);

        sub.Dispose();
        await bus.PublishAsync(new TestEvent { Data = "after" });
        handler.Received.Should().HaveCount(1);
    }

    [Fact]
    public async Task PublishAsync_ConcurrentPublish_AllEventsDelivered()
    {
        InMemoryEventBus bus = new();
        int count = 0;

        bus.Subscribe<TestEvent>(
            (_, _) =>
            {
                Interlocked.Increment(ref count);
                return Task.CompletedTask;
            }
        );

        Task[] tasks = Enumerable
            .Range(0, 100)
            .Select(i => bus.PublishAsync(new TestEvent { Data = $"event-{i}" }))
            .ToArray();

        await Task.WhenAll(tasks);

        count.Should().Be(100);
    }
}
