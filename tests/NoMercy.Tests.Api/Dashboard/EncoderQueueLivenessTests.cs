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

using NoMercy.Api.Controllers.V1.Dashboard.Admin;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// "Encoding now" and "waiting" is the split the whole dashboard hangs off — one
/// screen on mobile each, two sections on the web panel. A coordinator stamps
/// itself the moment it has decomposed and queued a bundle, so with one runner
/// every episode of a season claims to be in flight simultaneously. Only a held
/// reservation means a runner is actually on it.
/// </summary>
[Trait("Category", "Unit")]
public class EncoderQueueLivenessTests
{
    [Fact]
    public void CoordinatorWhoseChildIsReserved_IsInFlight()
    {
        TasksController.IsEncodeInFlight(null, hasReservedChild: true).Should().BeTrue();
    }

    [Fact]
    public void ReservedCoordinatorRow_IsInFlight()
    {
        TasksController.IsEncodeInFlight(DateTime.UtcNow, hasReservedChild: false).Should().BeTrue();
    }

    [Fact]
    public void DecomposedButWaitingForARunner_IsStillQueued()
    {
        TasksController.IsEncodeInFlight(null, hasReservedChild: false).Should().BeFalse();
    }
}
