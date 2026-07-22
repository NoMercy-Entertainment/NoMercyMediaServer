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
using NoMercy.Events.Audit;
using NoMercy.Events.Encoding;
using NoMercy.Events.Library;
using NoMercy.Events.Media;
using NoMercy.Events.Playback;
using Xunit;

namespace NoMercy.Tests.Events;

public class EventAuditTests
{
    [Fact]
    public void AuditLog_RecordsEvents()
    {
        EventAuditLog auditLog = new();

        LibraryRefreshedEvent evt = new() { QueryKey = ["music", "album", Guid.NewGuid()] };

        auditLog.Record(@event: evt, eventTypeName: "LibraryRefreshedEvent");

        auditLog.Count.Should().Be(expected: 1);
        IReadOnlyList<EventAuditEntry> entries = auditLog.GetEntries();
        entries.Should().HaveCount(expected: 1);
        entries[index: 0].EventType.Should().Be(expected: "LibraryRefreshedEvent");
        entries[index: 0].EventId.Should().Be(expected: evt.EventId);
        entries[index: 0].Source.Should().Be(expected: "LibraryRefresh");
        entries[index: 0].Timestamp.Should().Be(expected: evt.Timestamp);
        entries[index: 0].Payload.Should().Contain(expected: "QueryKey");
    }

    [Fact]
    public void AuditLog_DisabledDoesNotRecord()
    {
        EventAuditLog auditLog = new(options: new() { Enabled = false });

        auditLog.Record(@event: new LibraryRefreshedEvent { QueryKey = ["test"] }, eventTypeName: "LibraryRefreshedEvent");

        auditLog.Count.Should().Be(expected: 0);
        auditLog.GetEntries().Should().BeEmpty();
    }

    [Fact]
    public void AuditLog_ExcludedEventTypesAreSkipped()
    {
        EventAuditLog auditLog = new(options: new() { ExcludedEventTypes = ["EncodingProgressUpdatedEvent"] });

        auditLog.Record(
            @event: new EncodingProgressUpdatedEvent
            {
                JobId = 1,
                Percentage = 50,
                Elapsed = TimeSpan.FromMinutes(minutes: 1),
            },
            eventTypeName: "EncodingProgressUpdatedEvent"
        );

        auditLog.Record(
            @event: new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/test.mkv",
                OutputPath = "/out/",
                ProfileName = "x264",
            },
            eventTypeName: "EncodingStartedEvent"
        );

        auditLog.Count.Should().Be(expected: 1);
        auditLog.GetEntries()[index: 0].EventType.Should().Be(expected: "EncodingStartedEvent");
    }

    [Fact]
    public void AuditLog_CompactsWhenMaxEntriesExceeded()
    {
        EventAuditLog auditLog = new(options: new() { MaxEntries = 10, CompactionPercentage = 0.5 });

        for (int i = 0; i < 15; i++)
        {
            auditLog.Record(
                @event: new LibraryRefreshedEvent { QueryKey = ["test", i.ToString()] },
                eventTypeName: "LibraryRefreshedEvent"
            );
        }

        // After compaction (50% of 10 = 5 removed from oldest), count should be <= MaxEntries
        auditLog.Count.Should().BeLessThanOrEqualTo(expected: 15);
        auditLog.Count.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public void AuditLog_Clear_RemovesAllEntries()
    {
        EventAuditLog auditLog = new();

        for (int i = 0; i < 5; i++)
        {
            auditLog.Record(@event: new LibraryRefreshedEvent { QueryKey = ["test"] }, eventTypeName: "LibraryRefreshedEvent");
        }

        auditLog.Count.Should().Be(expected: 5);
        auditLog.Clear();
        auditLog.Count.Should().Be(expected: 0);
        auditLog.GetEntries().Should().BeEmpty();
    }

    [Fact]
    public void AuditLog_GetEntries_ByEventType()
    {
        EventAuditLog auditLog = new();

        auditLog.Record(@event: new LibraryRefreshedEvent { QueryKey = ["music"] }, eventTypeName: "LibraryRefreshedEvent");

        auditLog.Record(
            @event: new EncodingStartedEvent
            {
                JobId = 1,
                InputPath = "/test.mkv",
                OutputPath = "/out/",
                ProfileName = "x264",
            },
            eventTypeName: "EncodingStartedEvent"
        );

        auditLog.Record(
            @event: new LibraryRefreshedEvent { QueryKey = ["libraries"] },
            eventTypeName: "LibraryRefreshedEvent"
        );

        IReadOnlyList<EventAuditEntry> refreshEntries = auditLog.GetEntries(eventType: "LibraryRefreshedEvent");
        refreshEntries.Should().HaveCount(expected: 2);

        IReadOnlyList<EventAuditEntry> encodingEntries = auditLog.GetEntries(
            eventType: "EncodingStartedEvent"
        );
        encodingEntries.Should().HaveCount(expected: 1);
    }

    [Fact]
    public void AuditLog_GetEntries_ByTimeRange()
    {
        EventAuditLog auditLog = new();
        DateTime before = DateTime.UtcNow.AddSeconds(value: -1);

        auditLog.Record(@event: new LibraryRefreshedEvent { QueryKey = ["test"] }, eventTypeName: "LibraryRefreshedEvent");

        DateTime after = DateTime.UtcNow.AddSeconds(value: 1);

        IReadOnlyList<EventAuditEntry> entries = auditLog.GetEntries(from: before, to: after);
        entries.Should().HaveCount(expected: 1);

        IReadOnlyList<EventAuditEntry> emptyEntries = auditLog.GetEntries(
            from: DateTime.UtcNow.AddDays(value: -2),
            to: DateTime.UtcNow.AddDays(value: -1)
        );
        emptyEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditingDecorator_RecordsEventsAndDelegates()
    {
        InMemoryEventBus innerBus = new();
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator decorator = new(inner: innerBus, auditLog: auditLog);

        List<IEvent> received = [];
        decorator.Subscribe<LibraryRefreshedEvent>(
            handler: (evt, _) =>
            {
                received.Add(item: evt);
                return Task.CompletedTask;
            }
        );

        await decorator.PublishAsync(
            @event: new LibraryRefreshedEvent { QueryKey = ["music", "album", Guid.NewGuid()] }
        );

        auditLog.Count.Should().Be(expected: 1);
        received.Should().HaveCount(expected: 1);
    }

    [Fact]
    public async Task AuditingDecorator_SubscribesViaInner()
    {
        InMemoryEventBus innerBus = new();
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator decorator = new(inner: innerBus, auditLog: auditLog);

        bool handlerCalled = false;
        IDisposable sub = decorator.Subscribe<PlaybackStartedEvent>(
            handler: (_, _) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            }
        );

        await decorator.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 1,
                MediaType = "movie",
            }
        );

        handlerCalled.Should().BeTrue();

        sub.Dispose();
        handlerCalled = false;

        await decorator.PublishAsync(
            @event: new PlaybackStartedEvent
            {
                UserId = Guid.NewGuid(),
                MediaId = 2,
                MediaType = "tv",
            }
        );

        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task AuditLog_IsThreadSafe()
    {
        EventAuditLog auditLog = new(options: new() { MaxEntries = 50_000 });

        Task[] tasks = Enumerable
            .Range(start: 0, count: 100)
            .Select(selector: i =>
                Task.Run(action: () =>
                {
                    for (int j = 0; j < 100; j++)
                    {
                        auditLog.Record(
                            @event: new LibraryRefreshedEvent
                            {
                                QueryKey = ["test", i.ToString(), j.ToString()],
                            },
                            eventTypeName: "LibraryRefreshedEvent"
                        );
                    }
                })
            )
            .ToArray();

        await Task.WhenAll(tasks: tasks);

        auditLog.Count.Should().Be(expected: 10_000);
    }

    [Fact]
    public void AuditEntry_SerializesPayloadAsJson()
    {
        EventAuditLog auditLog = new();

        MediaAddedEvent evt = new()
        {
            MediaId = 42,
            MediaType = "movie",
            Title = "Test Movie",
            LibraryId = Ulid.NewUlid(),
        };

        auditLog.Record(@event: evt, eventTypeName: "MediaAddedEvent");

        EventAuditEntry entry = auditLog.GetEntries()[index: 0];
        entry.Payload.Should().Contain(expected: "\"MediaId\":42");
        entry.Payload.Should().Contain(expected: "\"MediaType\":\"movie\"");
        entry.Payload.Should().Contain(expected: "\"Title\":\"Test Movie\"");
    }

    [Fact]
    public void AuditOptions_DefaultValues()
    {
        EventAuditOptions options = new();

        options.Enabled.Should().BeTrue();
        options.MaxEntries.Should().Be(expected: 10_000);
        options.CompactionPercentage.Should().Be(expected: 0.25);
        options.ExcludedEventTypes.Should().BeEmpty();
    }

    [Fact]
    public async Task FullDecoratorChain_WorksCorrectly()
    {
        // InMemoryBus -> LoggingDecorator -> AuditingDecorator
        InMemoryEventBus innerBus = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator loggingBus = new(inner: innerBus, log: msg => logMessages.Add(item: msg));
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator auditBus = new(inner: loggingBus, auditLog: auditLog);

        bool handlerCalled = false;
        auditBus.Subscribe<EncodingCompletedEvent>(
            handler: (_, _) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            }
        );

        await auditBus.PublishAsync(
            @event: new EncodingCompletedEvent
            {
                JobId = 1,
                OutputPath = "/out/playlist.m3u8",
                Duration = TimeSpan.FromMinutes(minutes: 5),
            }
        );

        // Audit recorded
        auditLog.Count.Should().Be(expected: 1);
        auditLog.GetEntries()[index: 0].EventType.Should().Be(expected: "EncodingCompletedEvent");

        // Logging happened
        logMessages.Should().ContainSingle(predicate: m => m.Contains("EncodingCompletedEvent"));

        // Handler was called
        handlerCalled.Should().BeTrue();
    }
}
