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
using NoMercy.Events.Encoding;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.Events.Playback;
using Xunit;

namespace NoMercy.Tests.Events;

public class LoggingEventBusDecoratorTests
{
    private sealed class TestEvent : EventBase
    {
        public override string Source => "TestSource";
        public string Data { get; init; } = string.Empty;
    }

    [Fact]
    public async Task PublishAsync_LogsEventTypeName()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: msg => logMessages.Add(item: msg));

        await decorator.PublishAsync(@event: new TestEvent { Data = "hello" });

        logMessages.Should().ContainSingle();
        logMessages[index: 0].Should().Contain(expected: "TestEvent");
    }

    [Fact]
    public async Task PublishAsync_LogsEventSource()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: msg => logMessages.Add(item: msg));

        await decorator.PublishAsync(@event: new TestEvent { Data = "hello" });

        logMessages[index: 0].Should().Contain(expected: "Source=TestSource");
    }

    [Fact]
    public async Task PublishAsync_LogsEventId()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: msg => logMessages.Add(item: msg));

        TestEvent evt = new() { Data = "hello" };
        await decorator.PublishAsync(@event: evt);

        logMessages[index: 0].Should().Contain(expected: $"EventId={evt.EventId}");
    }

    [Fact]
    public async Task PublishAsync_LogsTimestamp()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: msg => logMessages.Add(item: msg));

        await decorator.PublishAsync(@event: new TestEvent { Data = "hello" });

        logMessages[index: 0].Should().Contain(expected: "Timestamp=");
    }

    [Fact]
    public async Task PublishAsync_DelegatesSubscribersToInnerBus()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: msg => logMessages.Add(item: msg));

        List<TestEvent> received = [];
        decorator.Subscribe<TestEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        TestEvent testEvent = new() { Data = "test-data" };
        await decorator.PublishAsync(@event: testEvent);

        received.Should().ContainSingle().Which.Data.Should().Be(expected: "test-data");
        logMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishAsync_LogsEachEventSeparately()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: msg => logMessages.Add(item: msg));

        await decorator.PublishAsync(@event: new TestEvent { Data = "first" });
        await decorator.PublishAsync(@event: new TestEvent { Data = "second" });
        await decorator.PublishAsync(@event: new TestEvent { Data = "third" });

        logMessages.Should().HaveCount(expected: 3);
    }

    [Fact]
    public async Task PublishAsync_LogsDifferentEventTypes()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: msg => logMessages.Add(item: msg));

        await decorator.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie",
            }
        );

        await decorator.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/a.mkv",
                OutputPath = "/out/",
                ProfileName = "x264",
            }
        );

        await decorator.PublishAsync(
            @event: new LibraryScanStartedEvent { LibraryId = Ulid.NewUlid(), LibraryName = "Movies" }
        );

        logMessages.Should().HaveCount(expected: 3);
        logMessages[index: 0].Should().Contain(expected: "PlaybackStartedEvent").And.Contain(expected: "Source=Playback");
        logMessages[index: 1].Should().Contain(expected: "EncodingStartedEvent").And.Contain(expected: "Source=Encoder");
        logMessages[index: 2]
            .Should()
            .Contain(expected: "LibraryScanStartedEvent")
            .And.Contain(expected: "Source=LibraryScanner");
    }

    [Fact]
    public async Task Subscribe_ReturnsDisposable_UnsubscribesOnDispose()
    {
        InMemoryEventBus inner = new();
        LoggingEventBusDecorator decorator = new(inner: inner, log: _ => { });

        List<TestEvent> received = [];
        IDisposable subscription = decorator.Subscribe<TestEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await decorator.PublishAsync(@event: new TestEvent { Data = "before" });
        received.Should().ContainSingle();

        subscription.Dispose();

        await decorator.PublishAsync(@event: new TestEvent { Data = "after" });
        received.Should().ContainSingle(because: "handler should not be called after dispose");
    }

    [Fact]
    public async Task Subscribe_WithEventHandler_DelegatesToInner()
    {
        InMemoryEventBus inner = new();
        LoggingEventBusDecorator decorator = new(inner: inner, log: _ => { });

        TestHandler handler = new();
        decorator.Subscribe(handler: handler);

        await decorator.PublishAsync(@event: new TestEvent { Data = "handler-test" });

        handler.Received.Should().ContainSingle().Which.Data.Should().Be(expected: "handler-test");
    }

    [Fact]
    public void Constructor_NullInner_Throws()
    {
        Action act = () => new LoggingEventBusDecorator(inner: null!, log: _ => { });
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "inner");
    }

    [Fact]
    public void Constructor_NullLog_Throws()
    {
        InMemoryEventBus inner = new();
        Action act = () => new LoggingEventBusDecorator(inner: inner, log: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "log");
    }

    [Fact]
    public async Task PublishAsync_LogsBeforeHandlersRun()
    {
        InMemoryEventBus inner = new();
        List<string> order = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: _ => order.Add(item: "logged"));

        decorator.Subscribe<TestEvent>(
            handler: (_, _) =>
            {
                order.Add(item: "handled");
                return Task.CompletedTask;
            }
        );

        await decorator.PublishAsync(@event: new TestEvent());

        order.Should().Equal(expected: ["logged", "handled"]);
    }

    [Fact]
    public async Task PublishAsync_PropagatesCancellation()
    {
        InMemoryEventBus inner = new();
        LoggingEventBusDecorator decorator = new(inner: inner, log: _ => { });
        CancellationTokenSource cts = new();

        decorator.Subscribe<TestEvent>(
            handler: (_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            }
        );

        decorator.Subscribe<TestEvent>(handler: (_, _) => Task.CompletedTask);

        Func<Task> act = () => decorator.PublishAsync(@event: new TestEvent(), ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PublishAsync_AllDomainEvents_AreLogged()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: msg => logMessages.Add(item: msg));

        Guid userId = Guid.NewGuid();
        Ulid libraryId = Ulid.NewUlid();

        await decorator.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = 1,
                MediaType = "movie",
            }
        );
        await decorator.PublishAsync(
            @event: new PlaybackProgressUpdatedEvent
            {
                UserId = userId,
                MediaId = 1,
                Position = TimeSpan.Zero,
                Duration = TimeSpan.Zero,
            }
        );
        await decorator.PublishAsync(
            @event: new PlaybackCompletedEvent
            {
                UserId = userId,
                MediaId = 1,
                MediaType = "movie",
            }
        );
        await decorator.PublishAsync(
            @event: new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/a",
                OutputPath = "/b",
                ProfileName = "x264",
            }
        );
        await decorator.PublishAsync(@event: new EncodingProgressUpdatedEvent { JobId = 1, Percentage = 50 });
        await decorator.PublishAsync(
            @event: new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/b",
                Duration = TimeSpan.Zero,
            }
        );
        await decorator.PublishAsync(
            @event: new EncodingFailedEvent
            {
                JobId = 1,
                InputPath = "/a",
                ErrorMessage = "err",
            }
        );
        await decorator.PublishAsync(
            @event: new LibraryScanStartedEvent { LibraryId = libraryId, LibraryName = "Movies" }
        );
        await decorator.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = libraryId,
                LibraryName = "Movies",
                ItemsFound = 0,
                Duration = TimeSpan.Zero,
            }
        );
        await decorator.PublishAsync(
            @event: new MediaAddedEvent
            {
                MediaId = 1,
                MediaType = "movie",
                Title = "T",
                LibraryId = libraryId,
            }
        );
        await decorator.PublishAsync(
            @event: new MediaRemovedEvent
            {
                MediaId = 1,
                MediaType = "movie",
                Title = "T",
                LibraryId = libraryId,
            }
        );

        logMessages.Should().HaveCount(expected: 11);
        logMessages.Should().OnlyContain(predicate: m => m.StartsWith("[Event]"));
        logMessages.Should().OnlyContain(predicate: m => m.Contains("EventId="));
        logMessages.Should().OnlyContain(predicate: m => m.Contains("Source="));
    }

    private sealed class TestHandler : IEventHandler<TestEvent>
    {
        public List<TestEvent> Received { get; } = [];

        public Task HandleAsync(TestEvent @event, CancellationToken ct = default)
        {
            Received.Add(item: @event);
            return Task.CompletedTask;
        }
    }
}
