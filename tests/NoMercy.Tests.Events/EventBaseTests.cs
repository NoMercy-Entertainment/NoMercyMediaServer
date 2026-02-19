using FluentAssertions;
using NoMercy.Events;
using Xunit;

namespace NoMercy.Tests.Events;

public class EventBaseTests
{
    private sealed class TestEvent : EventBase
    {
        public override string Source => "TestSource";
        public string Payload { get; init; } = string.Empty;
    }

    [Fact]
    public void EventBase_AssignsUniqueId()
    {
        TestEvent event1 = new();
        TestEvent event2 = new();

        event1.EventId.Should().NotBe(Guid.Empty);
        event2.EventId.Should().NotBe(Guid.Empty);
        event1.EventId.Should().NotBe(event2.EventId);
    }

    [Fact]
    public void EventBase_SetsTimestamp()
    {
        DateTime before = DateTime.UtcNow;
        TestEvent testEvent = new();
        DateTime after = DateTime.UtcNow;

        testEvent.Timestamp.Should().BeOnOrAfter(before);
        testEvent.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void EventBase_ImplementsIEvent()
    {
        TestEvent testEvent = new();

        IEvent asInterface = testEvent;
        asInterface.EventId.Should().Be(testEvent.EventId);
        asInterface.Timestamp.Should().Be(testEvent.Timestamp);
        asInterface.Source.Should().Be("TestSource");
    }

    [Fact]
    public void EventBase_DerivedClassCanAddProperties()
    {
        TestEvent testEvent = new() { Payload = "test-data" };

        testEvent.Payload.Should().Be("test-data");
        testEvent.Source.Should().Be("TestSource");
        testEvent.EventId.Should().NotBe(Guid.Empty);
    }
}
