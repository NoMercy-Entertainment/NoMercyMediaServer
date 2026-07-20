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
using NoMercy.Monitoring;
using Xunit;

namespace NoMercy.Tests.Monitoring;

/// <summary>
/// Requirement: a <see cref="Core"/> sample is one per-logical-core utilization
/// reading. Index and Utilization must round-trip independently of each other and
/// of any other sample in the same collection — a per-core reading may never leak
/// into or overwrite another core's slot.
/// </summary>
public class CoreTests
{
    [Fact]
    public void Core_DefaultValues_AreZero()
    {
        Core core = new();

        core.Index.Should().Be(0, "an unset core sample must not report a fabricated index");
        core.Utilization.Should().Be(0.0, "an unset core sample must not report fabricated load");
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 55.5)]
    [InlineData(15, 100.0)]
    [InlineData(3, 0.1)]
    public void Core_IndexAndUtilization_RoundTripIndependently(int index, double utilization)
    {
        Core core = new() { Index = index, Utilization = utilization };

        core.Index.Should().Be(index);
        core.Utilization.Should().Be(utilization);
    }

    [Fact]
    public void Core_MutatingOneInstance_DoesNotAffectAnother()
    {
        Core coreZero = new() { Index = 0, Utilization = 10.0 };
        Core coreOne = new() { Index = 1, Utilization = 90.0 };

        coreZero.Utilization = 20.0;

        coreZero.Index.Should().Be(0);
        coreZero.Utilization.Should().Be(20.0);
        coreOne.Index.Should().Be(1, "mutating one core sample must never move another's index");
        coreOne
            .Utilization.Should()
            .Be(90.0, "mutating one core sample must never move another's reading");
    }
}
