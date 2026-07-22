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

        event1.EventId.Should().NotBe(unexpected: Guid.Empty);
        event2.EventId.Should().NotBe(unexpected: Guid.Empty);
        event1.EventId.Should().NotBe(unexpected: event2.EventId);
    }

    [Fact]
    public void EventBase_SetsTimestamp()
    {
        DateTime before = DateTime.UtcNow;
        TestEvent testEvent = new();
        DateTime after = DateTime.UtcNow;

        testEvent.Timestamp.Should().BeOnOrAfter(expected: before);
        testEvent.Timestamp.Should().BeOnOrBefore(expected: after);
    }

    [Fact]
    public void EventBase_ImplementsIEvent()
    {
        TestEvent testEvent = new();

        IEvent asInterface = testEvent;
        asInterface.EventId.Should().Be(expected: testEvent.EventId);
        asInterface.Timestamp.Should().Be(expected: testEvent.Timestamp);
        asInterface.Source.Should().Be(expected: "TestSource");
    }

    [Fact]
    public void EventBase_DerivedClassCanAddProperties()
    {
        TestEvent testEvent = new() { Payload = "test-data" };

        testEvent.Payload.Should().Be(expected: "test-data");
        testEvent.Source.Should().Be(expected: "TestSource");
        testEvent.EventId.Should().NotBe(unexpected: Guid.Empty);
    }
}
