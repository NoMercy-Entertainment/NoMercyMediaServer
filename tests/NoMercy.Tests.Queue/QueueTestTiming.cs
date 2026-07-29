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

namespace NoMercy.Tests.Queue;

/// <summary>
/// How long a test here waits for background work to happen.
/// <para>
/// These tests start real workers and poll until something is reserved,
/// released or executed. Five seconds was generous on a developer's machine
/// and tight on a shared CI runner, where a worker competes with every other
/// test in the assembly for two cores — which produced timeouts with no
/// assertion behind them: the work was coming, the window closed first.
/// </para>
/// <para>
/// A test that fails under load is one people learn to re-run instead of read,
/// and re-running is how a real regression gets waved through. Raising the
/// window costs nothing when things work, because every wait returns the moment
/// its condition holds; only a genuine hang pays it.
/// </para>
/// </summary>
internal static class QueueTestTiming
{
    internal static TimeSpan WaitWindow { get; } = TimeSpan.FromSeconds(30);
}
