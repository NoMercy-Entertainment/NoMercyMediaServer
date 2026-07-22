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
using NoMercy.Events.Encoding;
using NoMercy.Events.Playback;
using Xunit;

namespace NoMercy.Tests.Events;

public class LoggingDecoratorExclusionTests
{
    private sealed class HighFrequencyEvent : EventBase
    {
        public override string Source => "Ticker";
        public int Tick { get; init; }
    }

    private sealed class ImportantEvent : EventBase
    {
        public override string Source => "System";
        public required string Message { get; init; }
    }

    [Fact]
    public async Task LoggingDecorator_ExcludedType_SuppressesLogButStillDelivers()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(
            inner: inner,
            log: msg => logMessages.Add(item: msg),
            excludedEventTypes: ["HighFrequencyEvent"]
        );

        List<HighFrequencyEvent> received = [];
        decorator.Subscribe<HighFrequencyEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await decorator.PublishAsync(@event: new HighFrequencyEvent { Tick = 1 });
        await decorator.PublishAsync(@event: new HighFrequencyEvent { Tick = 2 });

        logMessages.Should().BeEmpty(because: "excluded types must not be logged");
        received
            .Should()
            .HaveCount(
                expected: 2,
                because: "event must still be delivered to handlers even when excluded from logging"
            );
        received[index: 0].Tick.Should().Be(expected: 1);
        received[index: 1].Tick.Should().Be(expected: 2);
    }

    [Fact]
    public async Task LoggingDecorator_ExcludedType_DoesNotSuppressOtherTypes()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(
            inner: inner,
            log: msg => logMessages.Add(item: msg),
            excludedEventTypes: ["HighFrequencyEvent"]
        );

        await decorator.PublishAsync(@event: new HighFrequencyEvent { Tick = 99 });
        await decorator.PublishAsync(@event: new ImportantEvent { Message = "critical alert" });

        logMessages.Should().ContainSingle();
        logMessages[index: 0].Should().Contain(expected: "ImportantEvent");
        logMessages[index: 0].Should().NotContain(unexpected: "HighFrequencyEvent");
    }

    [Fact]
    public async Task LoggingDecorator_MultipleExcludedTypes_SuppressesAll()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(
            inner: inner,
            log: msg => logMessages.Add(item: msg),
            excludedEventTypes: ["HighFrequencyEvent", "EncodingProgressUpdatedEvent"]
        );

        await decorator.PublishAsync(@event: new HighFrequencyEvent { Tick = 1 });
        await decorator.PublishAsync(
            @event: new EncodingProgressUpdatedEvent
            {
                JobId = 1,
                Percentage = 50.0,
                Elapsed = TimeSpan.FromMinutes(minutes: 1),
            }
        );
        await decorator.PublishAsync(@event: new ImportantEvent { Message = "kept" });

        logMessages.Should().ContainSingle();
        logMessages[index: 0].Should().Contain(expected: "ImportantEvent");
    }

    [Fact]
    public async Task LoggingDecorator_EmptyExcludedList_LogsAllEvents()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(
            inner: inner,
            log: msg => logMessages.Add(item: msg),
            excludedEventTypes: []
        );

        await decorator.PublishAsync(@event: new HighFrequencyEvent { Tick = 1 });
        await decorator.PublishAsync(@event: new ImportantEvent { Message = "msg" });

        logMessages.Should().HaveCount(expected: 2);
    }

    [Fact]
    public async Task LoggingDecorator_NullExcludedList_LogsAllEvents()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner: inner, log: msg => logMessages.Add(item: msg), excludedEventTypes: null);

        await decorator.PublishAsync(@event: new HighFrequencyEvent { Tick = 1 });

        logMessages.Should().ContainSingle();
        logMessages[index: 0].Should().Contain(expected: "HighFrequencyEvent");
    }

    [Fact]
    public async Task InMemoryBus_WithLogger_HandlerThrows_LogsErrorAndContinuesOtherHandlers()
    {
        List<string> loggedErrors = [];

        ILogger<InMemoryEventBus> logger =
            NullLoggerFactory.Instance.CreateLogger<InMemoryEventBus>();

        InMemoryEventBus bus = new(logger: logger);
        List<string> executionOrder = [];

        bus.Subscribe<PlaybackStartedEvent>(
            handler: (_, _) =>
            {
                executionOrder.Add(item: "throws");
                throw new InvalidOperationException(message: "simulated handler crash");
            }
        );

        bus.Subscribe<PlaybackStartedEvent>(
            handler: (_, _) =>
            {
                executionOrder.Add(item: "recovers");
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie",
            }
        );

        executionOrder.Should().HaveCount(expected: 2, because: "both handlers ran — first threw, second recovered");
        executionOrder[index: 0].Should().Be(expected: "throws");
        executionOrder[index: 1].Should().Be(expected: "recovers");
    }

    [Fact]
    public async Task InMemoryBus_HandlerThrowsOnce_OtherHandlerReceivesCorrectPayload()
    {
        InMemoryEventBus bus = new();
        PlaybackStartedEvent? payloadSeen = null;

        bus.Subscribe<PlaybackStartedEvent>(handler: (_, _) => throw new(message: "bang"));

        bus.Subscribe<PlaybackStartedEvent>(
            handler: (evt, _) =>
            {
                payloadSeen = evt;
                return Task.CompletedTask;
            }
        );

        Guid userId = Guid.NewGuid();

        await bus.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = 42,
                MediaType = "tv",
            }
        );

        payloadSeen.Should().NotBeNull();
        payloadSeen!.UserId.Should().Be(expected: userId);
        payloadSeen.MediaId.Should().Be(expected: 42);
        payloadSeen.MediaType.Should().Be(expected: "tv");
    }

    [Fact]
    public async Task InMemoryBus_AllHandlersThrow_PublishDoesNotPropagateException()
    {
        InMemoryEventBus bus = new();

        bus.Subscribe<ImportantEvent>(handler: (_, _) => throw new ApplicationException(message: "first"));
        bus.Subscribe<ImportantEvent>(handler: (_, _) => throw new ApplicationException(message: "second"));

        Func<Task> act = () => bus.PublishAsync(@event: new ImportantEvent { Message = "test" });

        await act.Should()
            .NotThrowAsync(
                because: "individual handler exceptions are swallowed by the bus; only OperationCanceledException propagates"
            );
    }

    [Fact]
    public async Task InMemoryBus_CancelledAfterFirstHandler_SecondHandlerNeverRuns()
    {
        InMemoryEventBus bus = new();
        CancellationTokenSource cts = new();
        List<string> ran = [];

        bus.Subscribe<ImportantEvent>(
            handler: (_, _) =>
            {
                ran.Add(item: "first");
                cts.Cancel();
                return Task.CompletedTask;
            }
        );

        bus.Subscribe<ImportantEvent>(
            handler: (_, _) =>
            {
                ran.Add(item: "second");
                return Task.CompletedTask;
            }
        );

        Func<Task> act = () =>
            bus.PublishAsync(@event: new ImportantEvent { Message = "cancel-test" }, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        ran.Should().Equal(expected: "first");
        ran.Should().NotContain(unexpected: "second");
    }
}
