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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Events.Audit;
using Xunit;

namespace NoMercy.Tests.Events;

public class LogCapture
{
    public List<string> Messages { get; } = [];
}

public class LoggingCapture : ILogger<InMemoryEventBus>
{
    private readonly LogCapture _capture;

    public LoggingCapture(LogCapture capture)
    {
        _capture = capture;
    }

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _capture.Messages.Add(item: $"{logLevel}:{formatter(arg1: state, arg2: exception)}");
    }

    bool ILogger.IsEnabled(LogLevel logLevel) => true;

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;
}

public class ComprehensiveCoverageTests
{
    private sealed class TestEvent : EventBase
    {
        public override string Source => "Test";
        public string Data { get; init; } = string.Empty;
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

    private sealed class SerializationThrowingEvent : EventBase
    {
        public override string Source => "ThrowingTest";

        [System.Text.Json.Serialization.JsonConverter(converterType: typeof(ThrowingJsonConverter))]
        public string ThrowingProperty => "test";
    }

    private class ThrowingJsonConverter : System.Text.Json.Serialization.JsonConverter<string>
    {
        public override string Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        {
            return "";
        }

        public override void Write(System.Text.Json.Utf8JsonWriter writer, string value, System.Text.Json.JsonSerializerOptions options)
        {
            throw new NotSupportedException(message: "Intentional serialization failure");
        }
    }


    [Fact]
    public void EventBusProvider_ConfigureNull_ThrowsArgumentNullException()
    {
        Action act = () => EventBusProvider.Configure(eventBus: null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(paramName: "eventBus");
    }

    [Fact]
    public async Task InMemoryEventBus_WithNullLogger_DoesNotThrow()
    {
        InMemoryEventBus bus = new(logger: null);
        TestEvent evt = new() { Data = "test" };

        Func<Task> act = () => bus.PublishAsync(@event: evt);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InMemoryEventBus_WithLogger_HandlerThrowsLogsError()
    {
        LogCapture capture = new();
        LoggingCapture logger = new(capture: capture);
        InMemoryEventBus bus = new(logger: logger);

        bus.Subscribe<TestEvent>(
            handler: (_, _) => throw new InvalidOperationException(message: "test error")
        );

        await bus.PublishAsync(@event: new TestEvent { Data = "test" });

        capture.Messages.Should().ContainSingle();
        capture.Messages[index: 0].Should().Contain(expected: "Error:");
        capture.Messages[index: 0].Should().Contain(expected: "Event handler for TestEvent failed");
    }

    [Fact]
    public async Task InMemoryEventBus_WithLoggerNoError_DoesNotLogError()
    {
        LogCapture capture = new();
        LoggingCapture logger = new(capture: capture);
        InMemoryEventBus bus = new(logger: logger);

        bus.Subscribe<TestEvent>(
            handler: (_, _) => Task.CompletedTask
        );

        await bus.PublishAsync(@event: new TestEvent { Data = "test" });

        capture.Messages.Should().BeEmpty(because: "no errors logged");
    }

    [Fact]
    public async Task InMemoryEventBus_CancellationTokenThrowsBeforeHandler_PreventsFurtherHandlers()
    {
        InMemoryEventBus bus = new();
        CancellationTokenSource cts = new();
        List<string> order = [];

        bus.Subscribe<TestEvent>(
            handler: (_, ct) =>
            {
                order.Add(item: "first");
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        );

        bus.Subscribe<TestEvent>(
            handler: (_, _) =>
            {
                order.Add(item: "second");
                return Task.CompletedTask;
            }
        );

        Func<Task> act = () => bus.PublishAsync(@event: new TestEvent(), ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        order.Should().Equal(expected: "first");
    }

    [Fact]
    public async Task InMemoryEventBus_PublishAsync_GenericEventNoSubscribers_DoesNotThrow()
    {
        InMemoryEventBus bus = new();

        Func<Task> act = () => bus.PublishAsync(@event: new TestEvent());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void InMemoryEventBus_Subscribe_WithDelegateReturnsDisposable()
    {
        InMemoryEventBus bus = new();

        IDisposable sub = bus.Subscribe<TestEvent>(handler: (_, _) => Task.CompletedTask);

        sub.Should().NotBeNull();
        sub.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void InMemoryEventBus_Subscribe_WithEventHandlerReturnsDisposable()
    {
        InMemoryEventBus bus = new();
        TestHandler handler = new();

        IDisposable sub = bus.Subscribe(handler: handler);

        sub.Should().NotBeNull();
        sub.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public async Task InMemoryEventBus_DisposeSubscription_ThenDisposeAgain_IsIdempotent()
    {
        InMemoryEventBus bus = new();
        List<TestEvent> received = [];

        IDisposable sub = bus.Subscribe<TestEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(@event: new TestEvent { Data = "first" });
        received.Should().HaveCount(expected: 1);

        sub.Dispose();
        await bus.PublishAsync(@event: new TestEvent { Data = "second" });
        received.Should().HaveCount(expected: 1);

        sub.Dispose();
        await bus.PublishAsync(@event: new TestEvent { Data = "third" });
        received.Should().HaveCount(expected: 1, because: "double dispose should not affect anything");
    }

    [Fact]
    public async Task InMemoryEventBus_MultipleHandlers_AllReceiveTheSameEvent()
    {
        InMemoryEventBus bus = new();
        TestEvent evt = new() { Data = "shared" };

        List<TestEvent> received1 = [];
        List<TestEvent> received2 = [];
        List<TestEvent> received3 = [];

        bus.Subscribe<TestEvent>(handler: (e, _) =>
        {
            received1.Add(item: e);
            return Task.CompletedTask;
        });

        bus.Subscribe<TestEvent>(handler: (e, _) =>
        {
            received2.Add(item: e);
            return Task.CompletedTask;
        });

        bus.Subscribe<TestEvent>(handler: (e, _) =>
        {
            received3.Add(item: e);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(@event: evt);

        received1.Should().ContainSingle().Which.Should().BeSameAs(expected: evt);
        received2.Should().ContainSingle().Which.Should().BeSameAs(expected: evt);
        received3.Should().ContainSingle().Which.Should().BeSameAs(expected: evt);
    }

    [Fact]
    public async Task InMemoryEventBus_CancellationBetweenHandlers_StopsExecution()
    {
        InMemoryEventBus bus = new();
        CancellationTokenSource cts = new();

        List<string> order = [];
        bus.Subscribe<TestEvent>(
            handler: (_, ct) =>
            {
                order.Add(item: "first");
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        );

        bus.Subscribe<TestEvent>(
            handler: (_, _) =>
            {
                order.Add(item: "second");
                return Task.CompletedTask;
            }
        );

        Func<Task> act = () => bus.PublishAsync(@event: new TestEvent(), ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        order.Should().Equal(expected: "first");
    }

    [Fact]
    public async Task LoggingEventBusDecorator_Constructor_NullInner_Throws()
    {
        Action act = () => new LoggingEventBusDecorator(inner: null!, log: _ => { });

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(paramName: "inner");
    }

    [Fact]
    public async Task LoggingEventBusDecorator_Constructor_NullLog_Throws()
    {
        InMemoryEventBus inner = new();

        Action act = () => new LoggingEventBusDecorator(inner: inner, log: null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(paramName: "log");
    }

    [Fact]
    public async Task LoggingEventBusDecorator_PublishAsync_EventNotExcluded_Logs()
    {
        InMemoryEventBus inner = new();
        List<string> logs = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: logs.Add, excludedEventTypes: ["OtherEvent"]);

        await decorator.PublishAsync(@event: new TestEvent { Data = "test" });

        logs.Should().ContainSingle();
        logs[index: 0].Should().Contain(expected: "TestEvent");
    }

    [Fact]
    public async Task LoggingEventBusDecorator_PublishAsync_EventExcluded_DoesNotLog()
    {
        InMemoryEventBus inner = new();
        List<string> logs = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: logs.Add, excludedEventTypes: ["TestEvent"]);

        await decorator.PublishAsync(@event: new TestEvent { Data = "test" });

        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task LoggingEventBusDecorator_PublishAsync_ExcludedListNull_LogsAll()
    {
        InMemoryEventBus inner = new();
        List<string> logs = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: logs.Add, excludedEventTypes: null);

        await decorator.PublishAsync(@event: new TestEvent { Data = "test" });

        logs.Should().ContainSingle();
    }

    [Fact]
    public async Task LoggingEventBusDecorator_PublishAsync_EventExcluded_StillDelivers()
    {
        InMemoryEventBus inner = new();
        List<string> logs = [];
        List<TestEvent> received = [];

        LoggingEventBusDecorator decorator = new(inner: inner, log: logs.Add, excludedEventTypes: ["TestEvent"]);
        decorator.Subscribe<TestEvent>(handler: (evt, _) =>
        {
            received.Add(item: evt);
            return Task.CompletedTask;
        });

        await decorator.PublishAsync(@event: new TestEvent { Data = "test" });

        logs.Should().BeEmpty(because: "excluded event should not be logged");
        received.Should().ContainSingle(because: "excluded event should still be delivered");
    }

    [Fact]
    public async Task LoggingEventBusDecorator_Subscribe_Delegate_DelegatesToInner()
    {
        InMemoryEventBus inner = new();
        LoggingEventBusDecorator decorator = new(inner: inner, log: _ => { });

        List<TestEvent> received = [];
        IDisposable sub = decorator.Subscribe<TestEvent>(handler: (evt, _) =>
        {
            received.Add(item: evt);
            return Task.CompletedTask;
        });

        TestEvent evt = new() { Data = "test" };
        await decorator.PublishAsync(@event: evt);

        received.Should().ContainSingle().Which.Data.Should().Be(expected: "test");
        sub.Should().NotBeNull();
    }

    [Fact]
    public async Task LoggingEventBusDecorator_Subscribe_EventHandler_DelegatesToInner()
    {
        InMemoryEventBus inner = new();
        LoggingEventBusDecorator decorator = new(inner: inner, log: _ => { });

        TestHandler handler = new();
        IDisposable sub = decorator.Subscribe(handler: handler);

        TestEvent evt = new() { Data = "handler-test" };
        await decorator.PublishAsync(@event: evt);

        handler.Received.Should().ContainSingle().Which.Data.Should().Be(expected: "handler-test");
        sub.Should().NotBeNull();
    }

    [Fact]
    public async Task LoggingEventBusDecorator_PublishAsync_ExcludedEvents_CaseInsensitive()
    {
        InMemoryEventBus inner = new();
        List<string> logs = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: logs.Add, excludedEventTypes: ["testevent"]);

        await decorator.PublishAsync(@event: new TestEvent { Data = "test" });

        logs.Should().ContainSingle(because: "exclusion is case-sensitive, so this should be logged");
    }

    [Fact]
    public void AuditingEventBusDecorator_Constructor_NullInner_Throws()
    {
        EventAuditLog auditLog = new();

        Action act = () => new AuditingEventBusDecorator(inner: null!, auditLog: auditLog);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(paramName: "inner");
    }

    [Fact]
    public void AuditingEventBusDecorator_Constructor_NullAuditLog_Throws()
    {
        InMemoryEventBus inner = new();

        Action act = () => new AuditingEventBusDecorator(inner: inner, auditLog: null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(paramName: "auditLog");
    }

    [Fact]
    public async Task AuditingEventBusDecorator_PublishAsync_RecordsBeforeDelivering()
    {
        InMemoryEventBus inner = new();
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator decorator = new(inner: inner, auditLog: auditLog);

        List<int> order = [];
        decorator.Subscribe<TestEvent>(
            handler: (_, _) =>
            {
                order.Add(item: 2);
                return Task.CompletedTask;
            }
        );

        auditLog.Record(@event: new TestEvent(), eventTypeName: "TestEvent");
        order.Add(item: 1);

        TestEvent evt = new() { Data = "test" };
        await decorator.PublishAsync(@event: evt);

        auditLog.Count.Should().Be(expected: 2);
        order.Should().Equal(elements: [1, 2]);
    }

    [Fact]
    public async Task AuditingEventBusDecorator_PublishAsync_AuditedEvent_WithDisabledAudit()
    {
        InMemoryEventBus inner = new();
        EventAuditLog auditLog = new(options: new() { Enabled = false });
        AuditingEventBusDecorator decorator = new(inner: inner, auditLog: auditLog);

        List<TestEvent> received = [];
        decorator.Subscribe<TestEvent>(handler: (evt, _) =>
        {
            received.Add(item: evt);
            return Task.CompletedTask;
        });

        TestEvent evt = new() { Data = "test" };
        await decorator.PublishAsync(@event: evt);

        auditLog.Count.Should().Be(expected: 0, because: "audit is disabled");
        received.Should().ContainSingle(because: "event should still be delivered");
    }

    [Fact]
    public async Task AuditingEventBusDecorator_PublishAsync_AuditedEvent_WithExclusion()
    {
        InMemoryEventBus inner = new();
        EventAuditLog auditLog = new(options: new() { ExcludedEventTypes = ["TestEvent"] });
        AuditingEventBusDecorator decorator = new(inner: inner, auditLog: auditLog);

        List<TestEvent> received = [];
        decorator.Subscribe<TestEvent>(handler: (evt, _) =>
        {
            received.Add(item: evt);
            return Task.CompletedTask;
        });

        TestEvent evt = new() { Data = "test" };
        await decorator.PublishAsync(@event: evt);

        auditLog.Count.Should().Be(expected: 0, because: "TestEvent is excluded from audit");
        received.Should().ContainSingle(because: "event should still be delivered");
    }

    [Fact]
    public async Task AuditingEventBusDecorator_Subscribe_Delegate_DelegatesToInner()
    {
        InMemoryEventBus inner = new();
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator decorator = new(inner: inner, auditLog: auditLog);

        List<TestEvent> received = [];
        IDisposable sub = decorator.Subscribe<TestEvent>(handler: (evt, _) =>
        {
            received.Add(item: evt);
            return Task.CompletedTask;
        });

        TestEvent evt = new() { Data = "test" };
        await decorator.PublishAsync(@event: evt);

        received.Should().ContainSingle().Which.Data.Should().Be(expected: "test");
        sub.Should().NotBeNull();
    }

    [Fact]
    public async Task AuditingEventBusDecorator_Subscribe_EventHandler_DelegatesToInner()
    {
        InMemoryEventBus inner = new();
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator decorator = new(inner: inner, auditLog: auditLog);

        TestHandler handler = new();
        IDisposable sub = decorator.Subscribe(handler: handler);

        TestEvent evt = new() { Data = "handler-test" };
        await decorator.PublishAsync(@event: evt);

        handler.Received.Should().ContainSingle().Which.Data.Should().Be(expected: "handler-test");
        sub.Should().NotBeNull();
    }

    [Fact]
    public void AuditingEventBusDecorator_AuditLog_Property_ReturnsProvidedLog()
    {
        InMemoryEventBus inner = new();
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator decorator = new(inner: inner, auditLog: auditLog);

        decorator.AuditLog.Should().BeSameAs(expected: auditLog);
    }

    [Fact]
    public async Task InMemoryEventBus_ConcurrentPublish_AllEventDelivered()
    {
        InMemoryEventBus bus = new();
        int successCount = 0;

        bus.Subscribe<TestEvent>(handler: (_, _) =>
        {
            Interlocked.Increment(location: ref successCount);
            return Task.CompletedTask;
        });

        Task[] tasks = Enumerable.Range(start: 0, count: 50).Select(selector: i =>
            bus.PublishAsync(@event: new TestEvent { Data = $"event-{i}" })
        ).ToArray();

        await Task.WhenAll(tasks: tasks);

        successCount.Should().Be(expected: 50);
    }

    [Fact]
    public async Task InMemoryEventBus_PublishAsync_WithCancellationAfterHandler_RethrowsOperationCanceled()
    {
        InMemoryEventBus bus = new();
        CancellationTokenSource cts = new();

        bus.Subscribe<TestEvent>(
            handler: (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        );

        Func<Task> act = () => bus.PublishAsync(@event: new TestEvent(), ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InMemoryEventBus_RemoveSubscriber_DuringIteration_SafeSnapshot()
    {
        InMemoryEventBus bus = new();
        List<string> executed = [];

        IDisposable sub1 = bus.Subscribe<TestEvent>(
            handler: (_, _) =>
            {
                executed.Add(item: "handler1");
                return Task.CompletedTask;
            }
        );

        IDisposable sub2 = bus.Subscribe<TestEvent>(
            handler: (_, _) =>
            {
                executed.Add(item: "handler2");
                sub1.Dispose();
                return Task.CompletedTask;
            }
        );

        IDisposable sub3 = bus.Subscribe<TestEvent>(
            handler: (_, _) =>
            {
                executed.Add(item: "handler3");
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(@event: new TestEvent());

        executed.Should().Equal(expected: ["handler1", "handler2", "handler3"]);
    }

    [Fact]
    public async Task LoggingEventBusDecorator_LogFormat_ContainsAllRequiredFields()
    {
        InMemoryEventBus inner = new();
        List<string> logs = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: logs.Add);

        TestEvent evt = new() { Data = "test" };
        await decorator.PublishAsync(@event: evt);

        logs.Should().ContainSingle();
        string log = logs[index: 0];

        log.Should().StartWith(expected: "[Event]");
        log.Should().Contain(expected: "TestEvent");
        log.Should().Contain(expected: "Source=Test");
        log.Should().Contain(expected: $"EventId={evt.EventId}");
        log.Should().Contain(expected: "Timestamp=");
    }

    [Fact]
    public async Task EventAuditLog_Enabled_DefaultTrue()
    {
        EventAuditLog log = new();

        log.Enabled.Should().BeTrue();
    }

    [Fact]
    public void EventAuditLog_WithOptions_RespectsOptions()
    {
        EventAuditOptions options = new()
        {
            Enabled = false,
            MaxEntries = 5000,
            CompactionPercentage = 0.5
        };

        EventAuditLog log = new(options: options);

        log.Enabled.Should().BeFalse();
    }

    [Fact]
    public void EventAuditLog_Compact_RemovesOldestEntries()
    {
        EventAuditLog log = new(options: new() { MaxEntries = 10, CompactionPercentage = 0.5 });

        for (int i = 0; i < 12; i++)
        {
            log.Record(@event: new TestEvent { Data = i.ToString() }, eventTypeName: "TestEvent");
        }

        log.Count.Should().BeLessThanOrEqualTo(expected: 12);
        log.Count.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public void EventAuditLog_GetEntries_AllEntries()
    {
        EventAuditLog log = new();

        log.Record(@event: new TestEvent { Data = "1" }, eventTypeName: "TestEvent");
        log.Record(@event: new TestEvent { Data = "2" }, eventTypeName: "TestEvent");

        IReadOnlyList<EventAuditEntry> entries = log.GetEntries();

        entries.Should().HaveCount(expected: 2);
    }

    [Fact]
    public void EventAuditLog_GetEntries_ByType()
    {
        EventAuditLog log = new();

        log.Record(@event: new TestEvent { Data = "1" }, eventTypeName: "TestEvent");
        log.Record(@event: new TestEvent { Data = "2" }, eventTypeName: "OtherEvent");

        IReadOnlyList<EventAuditEntry> entries = log.GetEntries(eventType: "TestEvent");

        entries.Should().ContainSingle();
        entries[index: 0].EventType.Should().Be(expected: "TestEvent");
    }

    [Fact]
    public void EventAuditLog_GetEntries_ByTimeRange()
    {
        EventAuditLog log = new();
        DateTime before = DateTime.UtcNow.AddSeconds(value: -1);

        log.Record(@event: new TestEvent { Data = "1" }, eventTypeName: "TestEvent");

        DateTime after = DateTime.UtcNow.AddSeconds(value: 1);

        IReadOnlyList<EventAuditEntry> entries = log.GetEntries(from: before, to: after);

        entries.Should().ContainSingle();
    }

    [Fact]
    public void EventAuditLog_Clear_RemovesAllAndResetsCount()
    {
        EventAuditLog log = new();

        log.Record(@event: new TestEvent { Data = "1" }, eventTypeName: "TestEvent");
        log.Record(@event: new TestEvent { Data = "2" }, eventTypeName: "TestEvent");

        log.Count.Should().Be(expected: 2);

        log.Clear();

        log.Count.Should().Be(expected: 0);
        log.GetEntries().Should().BeEmpty();
    }

    [Fact]
    public void EventAuditLog_SerializationFailure_FallbackPayload()
    {
        EventAuditLog log = new();

        SerializationThrowingEvent evt = new();
        log.Record(@event: evt, eventTypeName: "SerializationThrowingEvent");

        EventAuditEntry entry = log.GetEntries()[index: 0];
        entry.Payload.Should().Contain(expected: "\"EventId\":");
        entry.Payload.Should().NotBeNull();
    }
}
