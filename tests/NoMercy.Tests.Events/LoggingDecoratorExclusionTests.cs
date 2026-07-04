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
            inner,
            msg => logMessages.Add(msg),
            excludedEventTypes: ["HighFrequencyEvent"]
        );

        List<HighFrequencyEvent> received = [];
        decorator.Subscribe<HighFrequencyEvent>(
            (evt, _) =>
            {
                received.Add(evt);
                return Task.CompletedTask;
            }
        );

        await decorator.PublishAsync(new HighFrequencyEvent { Tick = 1 });
        await decorator.PublishAsync(new HighFrequencyEvent { Tick = 2 });

        logMessages.Should().BeEmpty("excluded types must not be logged");
        received
            .Should()
            .HaveCount(
                2,
                "event must still be delivered to handlers even when excluded from logging"
            );
        received[0].Tick.Should().Be(1);
        received[1].Tick.Should().Be(2);
    }

    [Fact]
    public async Task LoggingDecorator_ExcludedType_DoesNotSuppressOtherTypes()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(
            inner,
            msg => logMessages.Add(msg),
            excludedEventTypes: ["HighFrequencyEvent"]
        );

        await decorator.PublishAsync(new HighFrequencyEvent { Tick = 99 });
        await decorator.PublishAsync(new ImportantEvent { Message = "critical alert" });

        logMessages.Should().ContainSingle();
        logMessages[0].Should().Contain("ImportantEvent");
        logMessages[0].Should().NotContain("HighFrequencyEvent");
    }

    [Fact]
    public async Task LoggingDecorator_MultipleExcludedTypes_SuppressesAll()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(
            inner,
            msg => logMessages.Add(msg),
            excludedEventTypes: ["HighFrequencyEvent", "EncodingProgressUpdatedEvent"]
        );

        await decorator.PublishAsync(new HighFrequencyEvent { Tick = 1 });
        await decorator.PublishAsync(
            new EncodingProgressUpdatedEvent
            {
                JobId = 1,
                Percentage = 50.0,
                Elapsed = TimeSpan.FromMinutes(1),
            }
        );
        await decorator.PublishAsync(new ImportantEvent { Message = "kept" });

        logMessages.Should().ContainSingle();
        logMessages[0].Should().Contain("ImportantEvent");
    }

    [Fact]
    public async Task LoggingDecorator_EmptyExcludedList_LogsAllEvents()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(
            inner,
            msg => logMessages.Add(msg),
            excludedEventTypes: []
        );

        await decorator.PublishAsync(new HighFrequencyEvent { Tick = 1 });
        await decorator.PublishAsync(new ImportantEvent { Message = "msg" });

        logMessages.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoggingDecorator_NullExcludedList_LogsAllEvents()
    {
        InMemoryEventBus inner = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator decorator = new(inner, msg => logMessages.Add(msg), null);

        await decorator.PublishAsync(new HighFrequencyEvent { Tick = 1 });

        logMessages.Should().ContainSingle();
        logMessages[0].Should().Contain("HighFrequencyEvent");
    }

    [Fact]
    public async Task InMemoryBus_WithLogger_HandlerThrows_LogsErrorAndContinuesOtherHandlers()
    {
        List<string> loggedErrors = [];

        ILogger<InMemoryEventBus> logger =
            NullLoggerFactory.Instance.CreateLogger<InMemoryEventBus>();

        InMemoryEventBus bus = new(logger);
        List<string> executionOrder = [];

        bus.Subscribe<PlaybackStartedEvent>(
            (_, _) =>
            {
                executionOrder.Add("throws");
                throw new InvalidOperationException("simulated handler crash");
            }
        );

        bus.Subscribe<PlaybackStartedEvent>(
            (_, _) =>
            {
                executionOrder.Add("recovers");
                return Task.CompletedTask;
            }
        );

        await bus.PublishAsync(
            new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie",
            }
        );

        executionOrder.Should().HaveCount(2, "both handlers ran — first threw, second recovered");
        executionOrder[0].Should().Be("throws");
        executionOrder[1].Should().Be("recovers");
    }

    [Fact]
    public async Task InMemoryBus_HandlerThrowsOnce_OtherHandlerReceivesCorrectPayload()
    {
        InMemoryEventBus bus = new();
        PlaybackStartedEvent? payloadSeen = null;

        bus.Subscribe<PlaybackStartedEvent>((_, _) => throw new Exception("bang"));

        bus.Subscribe<PlaybackStartedEvent>(
            (evt, _) =>
            {
                payloadSeen = evt;
                return Task.CompletedTask;
            }
        );

        Guid userId = Guid.NewGuid();

        await bus.PublishAsync(
            new PlaybackStartedEvent
            {
                UserId = userId,
                MediaId = 42,
                MediaType = "tv",
            }
        );

        payloadSeen.Should().NotBeNull();
        payloadSeen!.UserId.Should().Be(userId);
        payloadSeen.MediaId.Should().Be(42);
        payloadSeen.MediaType.Should().Be("tv");
    }

    [Fact]
    public async Task InMemoryBus_AllHandlersThrow_PublishDoesNotPropagateException()
    {
        InMemoryEventBus bus = new();

        bus.Subscribe<ImportantEvent>((_, _) => throw new ApplicationException("first"));
        bus.Subscribe<ImportantEvent>((_, _) => throw new ApplicationException("second"));

        Func<Task> act = () => bus.PublishAsync(new ImportantEvent { Message = "test" });

        await act.Should()
            .NotThrowAsync(
                "individual handler exceptions are swallowed by the bus; only OperationCanceledException propagates"
            );
    }

    [Fact]
    public async Task InMemoryBus_CancelledAfterFirstHandler_SecondHandlerNeverRuns()
    {
        InMemoryEventBus bus = new();
        CancellationTokenSource cts = new();
        List<string> ran = [];

        bus.Subscribe<ImportantEvent>(
            (_, _) =>
            {
                ran.Add("first");
                cts.Cancel();
                return Task.CompletedTask;
            }
        );

        bus.Subscribe<ImportantEvent>(
            (_, _) =>
            {
                ran.Add("second");
                return Task.CompletedTask;
            }
        );

        Func<Task> act = () =>
            bus.PublishAsync(new ImportantEvent { Message = "cancel-test" }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        ran.Should().Equal("first");
        ran.Should().NotContain("second");
    }
}
