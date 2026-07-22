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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Api.EventHandlers;
using NoMercy.Events;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.Networking.Messaging;
using Xunit;

namespace NoMercy.Tests.Api.EventHandlers;

public class SignalRLibraryScanEventHandlerJourneyTests
{
    private sealed record Capture(string Method, string Hub, object? Payload);

    private static (
        InMemoryEventBus bus,
        List<Capture> calls,
        SignalRLibraryScanEventHandler handler
    ) BuildScanChain()
    {
        InMemoryEventBus bus = new();
        List<Capture> calls = [];
        Mock<IClientMessenger> messengerMock = new(behavior: MockBehavior.Loose);
        messengerMock
            .Setup(expression: m => m.SendToAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, string, object>(
                action: (method, hub, payload) => calls.Add(item: new(Method: method, Hub: hub, Payload: payload))
            )
            .Returns(value: Task.CompletedTask);
        SignalRLibraryScanEventHandler handler = new(
            logger: NullLogger<SignalRLibraryScanEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: messengerMock.Object
        );
        return (bus, calls, handler);
    }

    private static T? GetProp<T>(object? obj, string name)
    {
        if (obj is null)
            return default;
        object? val = obj.GetType().GetProperty(name: name)?.GetValue(obj: obj);
        if (val is T typed)
            return typed;
        return default;
    }

    [Fact]
    public async Task LibraryScanStarted_PublishViaRealBus_CallsSendToAll_DashboardHub_LibraryScanStarted_WithCorrectPayload()
    {
        (InMemoryEventBus bus, List<Capture> calls, SignalRLibraryScanEventHandler handler) =
            BuildScanChain();
        using SignalRLibraryScanEventHandler _ = handler;

        Ulid libraryId = Ulid.NewUlid();
        await bus.PublishAsync(
            @event: new LibraryScanStartedEvent { LibraryId = libraryId, LibraryName = "Movies" }
        );

        Capture call = calls.Should().ContainSingle().Which;
        call.Method.Should().Be(expected: "LibraryScanStarted");
        call.Hub.Should().Be(expected: "dashboardHub");
        GetProp<string>(obj: call.Payload, name: "LibraryId").Should().Be(expected: libraryId.ToString());
        GetProp<string>(obj: call.Payload, name: "LibraryName").Should().Be(expected: "Movies");
    }

    [Fact]
    public async Task LibraryScanCompleted_PublishViaRealBus_CallsSendToAll_DashboardHub_LibraryScanCompleted_WithCorrectPayload()
    {
        (InMemoryEventBus bus, List<Capture> calls, SignalRLibraryScanEventHandler handler) =
            BuildScanChain();
        using SignalRLibraryScanEventHandler _ = handler;

        Ulid libraryId = Ulid.NewUlid();
        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = libraryId,
                LibraryName = "TV Shows",
                ItemsFound = 42,
                Duration = TimeSpan.FromSeconds(seconds: 15),
            }
        );

        Capture call = calls.Should().ContainSingle().Which;
        call.Method.Should().Be(expected: "LibraryScanCompleted");
        call.Hub.Should().Be(expected: "dashboardHub");
        GetProp<string>(obj: call.Payload, name: "LibraryId").Should().Be(expected: libraryId.ToString());
        GetProp<string>(obj: call.Payload, name: "LibraryName").Should().Be(expected: "TV Shows");
        GetProp<int>(obj: call.Payload, name: "ItemsFound").Should().Be(expected: 42);
        GetProp<double>(obj: call.Payload, name: "Duration").Should().BeApproximately(expectedValue: 15.0, precision: 0.001);
    }

    [Fact]
    public async Task MediaAdded_PublishViaRealBus_CallsSendToAll_DashboardHub_MediaAdded_WithCorrectPayload()
    {
        (InMemoryEventBus bus, List<Capture> calls, SignalRLibraryScanEventHandler handler) =
            BuildScanChain();
        using SignalRLibraryScanEventHandler _ = handler;

        Ulid libraryId = Ulid.NewUlid();
        await bus.PublishAsync(
            @event: new MediaAddedEvent
            {
                MediaId = 999,
                MediaType = "movie",
                Title = "Inception",
                LibraryId = libraryId,
            }
        );

        Capture call = calls.Should().ContainSingle().Which;
        call.Method.Should().Be(expected: "MediaAdded");
        call.Hub.Should().Be(expected: "dashboardHub");
        GetProp<int>(obj: call.Payload, name: "MediaId").Should().Be(expected: 999);
        GetProp<string>(obj: call.Payload, name: "MediaType").Should().Be(expected: "movie");
        GetProp<string>(obj: call.Payload, name: "Title").Should().Be(expected: "Inception");
        GetProp<string>(obj: call.Payload, name: "LibraryId").Should().Be(expected: libraryId.ToString());
    }

    [Fact]
    public async Task MediaRemoved_PublishViaRealBus_CallsSendToAll_DashboardHub_MediaRemoved_WithCorrectPayload()
    {
        (InMemoryEventBus bus, List<Capture> calls, SignalRLibraryScanEventHandler handler) =
            BuildScanChain();
        using SignalRLibraryScanEventHandler _ = handler;

        Ulid libraryId = Ulid.NewUlid();
        await bus.PublishAsync(
            @event: new MediaRemovedEvent
            {
                MediaId = 7,
                MediaType = "tv",
                Title = "Breaking Bad",
                LibraryId = libraryId,
            }
        );

        Capture call = calls.Should().ContainSingle().Which;
        call.Method.Should().Be(expected: "MediaRemoved");
        call.Hub.Should().Be(expected: "dashboardHub");
        GetProp<int>(obj: call.Payload, name: "MediaId").Should().Be(expected: 7);
        GetProp<string>(obj: call.Payload, name: "MediaType").Should().Be(expected: "tv");
        GetProp<string>(obj: call.Payload, name: "Title").Should().Be(expected: "Breaking Bad");
        GetProp<string>(obj: call.Payload, name: "LibraryId").Should().Be(expected: libraryId.ToString());
    }

    [Fact]
    public async Task AllFourEvents_EachCallSendToAll_ExactlyOnce_OnDashboardHub_ForTheirOwnMethod()
    {
        (InMemoryEventBus bus, List<Capture> calls, SignalRLibraryScanEventHandler handler) =
            BuildScanChain();
        using SignalRLibraryScanEventHandler _ = handler;

        Ulid id = Ulid.NewUlid();

        await bus.PublishAsync(@event: new LibraryScanStartedEvent { LibraryId = id, LibraryName = "L1" });
        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = id,
                LibraryName = "L1",
                ItemsFound = 1,
                Duration = TimeSpan.Zero,
            }
        );
        await bus.PublishAsync(
            @event: new MediaAddedEvent
            {
                MediaId = 1,
                MediaType = "movie",
                Title = "A",
                LibraryId = id,
            }
        );
        await bus.PublishAsync(
            @event: new MediaRemovedEvent
            {
                MediaId = 2,
                MediaType = "tv",
                Title = "B",
                LibraryId = id,
            }
        );

        calls.Should().HaveCount(expected: 4);
        calls.Should().OnlyContain(predicate: c => c.Hub == "dashboardHub");
        calls
            .Select(selector: c => c.Method)
            .Should()
            .ContainInOrder(expected: ["LibraryScanStarted", "LibraryScanCompleted", "MediaAdded", "MediaRemoved"]
            );
    }

    [Fact]
    public async Task Dispose_StopsAllDelivery_ForAllFourEvents()
    {
        (InMemoryEventBus bus, List<Capture> calls, SignalRLibraryScanEventHandler handler) =
            BuildScanChain();

        Ulid id = Ulid.NewUlid();
        await bus.PublishAsync(@event: new LibraryScanStartedEvent { LibraryId = id, LibraryName = "L" });

        calls.Should().HaveCount(expected: 1);

        handler.Dispose();

        await bus.PublishAsync(@event: new LibraryScanStartedEvent { LibraryId = id, LibraryName = "L2" });
        await bus.PublishAsync(
            @event: new LibraryScanCompletedEvent
            {
                LibraryId = id,
                LibraryName = "L",
                ItemsFound = 0,
                Duration = TimeSpan.Zero,
            }
        );

        calls.Should().HaveCount(expected: 1, because: "dispose removed all subscriptions");
    }
}

public class SignalRLibraryRefreshEventHandlerJourneyTests
{
    private sealed record Capture(string Method, string Hub, object? Payload);

    private static (
        InMemoryEventBus bus,
        List<Capture> calls,
        SignalRLibraryRefreshEventHandler handler
    ) BuildRefreshChain()
    {
        InMemoryEventBus bus = new();
        List<Capture> calls = [];
        Mock<IClientMessenger> messengerMock = new(behavior: MockBehavior.Loose);
        messengerMock
            .Setup(expression: m => m.SendToAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, string, object>(
                action: (method, hub, payload) => calls.Add(item: new(Method: method, Hub: hub, Payload: payload))
            )
            .Returns(value: Task.CompletedTask);
        SignalRLibraryRefreshEventHandler handler = new(eventBus: bus, clientMessenger: messengerMock.Object);
        return (bus, calls, handler);
    }

    [Fact]
    public async Task LibraryRefreshed_PublishViaRealBus_CallsSendToAll_VideoHub_RefreshLibrary_WithQueryKey()
    {
        (InMemoryEventBus bus, List<Capture> calls, SignalRLibraryRefreshEventHandler handler) =
            BuildRefreshChain();
        using SignalRLibraryRefreshEventHandler _ = handler;

        object?[] queryKey = ["movies", 1, null];
        await bus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = queryKey });

        Capture call = calls.Should().ContainSingle().Which;
        call.Method.Should().Be(expected: "RefreshLibrary");
        call.Hub.Should().Be(expected: "videoHub");
    }

    [Fact]
    public async Task LibraryRefreshed_DoesNotPublish_ToDashboardHub()
    {
        (InMemoryEventBus bus, List<Capture> calls, SignalRLibraryRefreshEventHandler handler) =
            BuildRefreshChain();
        using SignalRLibraryRefreshEventHandler _ = handler;

        await bus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["key"] });

        calls.Should().OnlyContain(predicate: c => c.Hub != "dashboardHub");
    }

    [Fact]
    public async Task Dispose_StopsDelivery_ForLibraryRefreshedEvent()
    {
        (InMemoryEventBus bus, List<Capture> calls, SignalRLibraryRefreshEventHandler handler) =
            BuildRefreshChain();

        await bus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["a"] });

        calls.Should().HaveCount(expected: 1);

        handler.Dispose();

        await bus.PublishAsync(@event: new LibraryRefreshedEvent { QueryKey = ["b"] });

        calls.Should().HaveCount(expected: 1, because: "dispose removed the subscription");
    }
}

public class LibraryScanCompletedFanOutJourneyTests
{
    [Fact]
    public async Task LibraryScanCompletedEvent_FansOut_ToBothSignalRHandlerAndTestListener_FromSinglePublish()
    {
        InMemoryEventBus bus = new();
        List<string> signalRCalls = [];
        Mock<IClientMessenger> messengerMock = new(behavior: MockBehavior.Loose);
        messengerMock
            .Setup(expression: m => m.SendToAll(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, string, object>(action: (method, _, _) => signalRCalls.Add(item: method))
            .Returns(value: Task.CompletedTask);

        using SignalRLibraryScanEventHandler scanHandler = new(
            logger: NullLogger<SignalRLibraryScanEventHandler>.Instance,
            eventBus: bus,
            clientMessenger: messengerMock.Object
        );

        List<LibraryScanCompletedEvent> receivedByTestListener = [];
        using IDisposable testSubscription = bus.Subscribe<LibraryScanCompletedEvent>(
            handler: (evt, _) =>
            {
                receivedByTestListener.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        Ulid libraryId = Ulid.NewUlid();
        LibraryScanCompletedEvent published = new()
        {
            LibraryId = libraryId,
            LibraryName = "Fan-Out Library",
            ItemsFound = 5,
            Duration = TimeSpan.FromSeconds(seconds: 3),
        };

        await bus.PublishAsync(@event: published);

        signalRCalls.Should().Contain(expected: "LibraryScanCompleted");
        receivedByTestListener.Should().ContainSingle();
        receivedByTestListener[index: 0].LibraryId.Should().Be(expected: libraryId);
    }
}
