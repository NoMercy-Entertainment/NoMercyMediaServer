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

public class EventBusProviderTests
{
    [Fact]
    public void Configure_SetsInstance()
    {
        InMemoryEventBus bus = new();

        EventBusProvider.Configure(bus);

        EventBusProvider.IsConfigured.Should().BeTrue();
        EventBusProvider.Current.Should().BeSameAs(bus);
    }

    [Fact]
    public void Configure_NullArg_ThrowsArgumentNullException()
    {
        Action act = () => EventBusProvider.Configure(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
