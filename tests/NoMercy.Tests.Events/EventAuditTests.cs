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

        LibraryRefreshEvent evt = new()
        {
            QueryKey = ["music", "album", Guid.NewGuid()]
        };

        auditLog.Record(evt, "LibraryRefreshEvent");

        auditLog.Count.Should().Be(1);
        IReadOnlyList<EventAuditEntry> entries = auditLog.GetEntries();
        entries.Should().HaveCount(1);
        entries[0].EventType.Should().Be("LibraryRefreshEvent");
        entries[0].EventId.Should().Be(evt.EventId);
        entries[0].Source.Should().Be("LibraryRefresh");
        entries[0].Timestamp.Should().Be(evt.Timestamp);
        entries[0].Payload.Should().Contain("QueryKey");
    }

    [Fact]
    public void AuditLog_DisabledDoesNotRecord()
    {
        EventAuditLog auditLog = new(new EventAuditOptions { Enabled = false });

        auditLog.Record(new LibraryRefreshEvent
        {
            QueryKey = ["test"]
        }, "LibraryRefreshEvent");

        auditLog.Count.Should().Be(0);
        auditLog.GetEntries().Should().BeEmpty();
    }

    [Fact]
    public void AuditLog_ExcludedEventTypesAreSkipped()
    {
        EventAuditLog auditLog = new(new EventAuditOptions
        {
            ExcludedEventTypes = ["EncodingProgressEvent"]
        });

        auditLog.Record(new EncodingProgressEvent
        {
            JobId = 1,
            Percentage = 50,
            Elapsed = TimeSpan.FromMinutes(1)
        }, "EncodingProgressEvent");

        auditLog.Record(new EncodingStartedEvent
        {
            JobId = 1,
            InputPath = "/test.mkv",
            OutputPath = "/out/",
            ProfileName = "x264"
        }, "EncodingStartedEvent");

        auditLog.Count.Should().Be(1);
        auditLog.GetEntries()[0].EventType.Should().Be("EncodingStartedEvent");
    }

    [Fact]
    public void AuditLog_CompactsWhenMaxEntriesExceeded()
    {
        EventAuditLog auditLog = new(new EventAuditOptions
        {
            MaxEntries = 10,
            CompactionPercentage = 0.5
        });

        for (int i = 0; i < 15; i++)
        {
            auditLog.Record(new LibraryRefreshEvent
            {
                QueryKey = ["test", i.ToString()]
            }, "LibraryRefreshEvent");
        }

        // After compaction (50% of 10 = 5 removed from oldest), count should be <= MaxEntries
        auditLog.Count.Should().BeLessThanOrEqualTo(15);
        auditLog.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AuditLog_Clear_RemovesAllEntries()
    {
        EventAuditLog auditLog = new();

        for (int i = 0; i < 5; i++)
        {
            auditLog.Record(new LibraryRefreshEvent
            {
                QueryKey = ["test"]
            }, "LibraryRefreshEvent");
        }

        auditLog.Count.Should().Be(5);
        auditLog.Clear();
        auditLog.Count.Should().Be(0);
        auditLog.GetEntries().Should().BeEmpty();
    }

    [Fact]
    public void AuditLog_GetEntries_ByEventType()
    {
        EventAuditLog auditLog = new();

        auditLog.Record(new LibraryRefreshEvent
        {
            QueryKey = ["music"]
        }, "LibraryRefreshEvent");

        auditLog.Record(new EncodingStartedEvent
        {
            JobId = 1,
            InputPath = "/test.mkv",
            OutputPath = "/out/",
            ProfileName = "x264"
        }, "EncodingStartedEvent");

        auditLog.Record(new LibraryRefreshEvent
        {
            QueryKey = ["libraries"]
        }, "LibraryRefreshEvent");

        IReadOnlyList<EventAuditEntry> refreshEntries = auditLog.GetEntries("LibraryRefreshEvent");
        refreshEntries.Should().HaveCount(2);

        IReadOnlyList<EventAuditEntry> encodingEntries = auditLog.GetEntries("EncodingStartedEvent");
        encodingEntries.Should().HaveCount(1);
    }

    [Fact]
    public void AuditLog_GetEntries_ByTimeRange()
    {
        EventAuditLog auditLog = new();
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        auditLog.Record(new LibraryRefreshEvent
        {
            QueryKey = ["test"]
        }, "LibraryRefreshEvent");

        DateTime after = DateTime.UtcNow.AddSeconds(1);

        IReadOnlyList<EventAuditEntry> entries = auditLog.GetEntries(before, after);
        entries.Should().HaveCount(1);

        IReadOnlyList<EventAuditEntry> emptyEntries = auditLog.GetEntries(
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddDays(-1));
        emptyEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditingDecorator_RecordsEventsAndDelegates()
    {
        InMemoryEventBus innerBus = new();
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator decorator = new(innerBus, auditLog);

        List<IEvent> received = [];
        decorator.Subscribe<LibraryRefreshEvent>((evt, _) =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        await decorator.PublishAsync(new LibraryRefreshEvent
        {
            QueryKey = ["music", "album", Guid.NewGuid()]
        });

        auditLog.Count.Should().Be(1);
        received.Should().HaveCount(1);
    }

    [Fact]
    public async Task AuditingDecorator_SubscribesViaInner()
    {
        InMemoryEventBus innerBus = new();
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator decorator = new(innerBus, auditLog);

        bool handlerCalled = false;
        IDisposable sub = decorator.Subscribe<PlaybackStartedEvent>((_, _) =>
        {
            handlerCalled = true;
            return Task.CompletedTask;
        });

        await decorator.PublishAsync(new PlaybackStartedEvent
        {
            UserId = Guid.NewGuid(),
            MediaId = 1,
            MediaType = "movie"
        });

        handlerCalled.Should().BeTrue();

        sub.Dispose();
        handlerCalled = false;

        await decorator.PublishAsync(new PlaybackStartedEvent
        {
            UserId = Guid.NewGuid(),
            MediaId = 2,
            MediaType = "tv"
        });

        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task AuditLog_IsThreadSafe()
    {
        EventAuditLog auditLog = new(new EventAuditOptions { MaxEntries = 50_000 });

        Task[] tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    auditLog.Record(new LibraryRefreshEvent
                    {
                        QueryKey = ["test", i.ToString(), j.ToString()]
                    }, "LibraryRefreshEvent");
                }
            })
        ).ToArray();

        await Task.WhenAll(tasks);

        auditLog.Count.Should().Be(10_000);
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
            LibraryId = Ulid.NewUlid()
        };

        auditLog.Record(evt, "MediaAddedEvent");

        EventAuditEntry entry = auditLog.GetEntries()[0];
        entry.Payload.Should().Contain("\"MediaId\":42");
        entry.Payload.Should().Contain("\"MediaType\":\"movie\"");
        entry.Payload.Should().Contain("\"Title\":\"Test Movie\"");
    }

    [Fact]
    public void AuditOptions_DefaultValues()
    {
        EventAuditOptions options = new();

        options.Enabled.Should().BeTrue();
        options.MaxEntries.Should().Be(10_000);
        options.CompactionPercentage.Should().Be(0.25);
        options.ExcludedEventTypes.Should().BeEmpty();
    }

    [Fact]
    public async Task FullDecoratorChain_WorksCorrectly()
    {
        // InMemoryBus -> LoggingDecorator -> AuditingDecorator
        InMemoryEventBus innerBus = new();
        List<string> logMessages = [];
        LoggingEventBusDecorator loggingBus = new(innerBus, msg => logMessages.Add(msg));
        EventAuditLog auditLog = new();
        AuditingEventBusDecorator auditBus = new(loggingBus, auditLog);

        bool handlerCalled = false;
        auditBus.Subscribe<EncodingCompletedEvent>((_, _) =>
        {
            handlerCalled = true;
            return Task.CompletedTask;
        });

        await auditBus.PublishAsync(new EncodingCompletedEvent
        {
            JobId = 1,
            OutputPath = "/out/playlist.m3u8",
            Duration = TimeSpan.FromMinutes(5)
        });

        // Audit recorded
        auditLog.Count.Should().Be(1);
        auditLog.GetEntries()[0].EventType.Should().Be("EncodingCompletedEvent");

        // Logging happened
        logMessages.Should().ContainSingle(m => m.Contains("EncodingCompletedEvent"));

        // Handler was called
        handlerCalled.Should().BeTrue();
    }
}
