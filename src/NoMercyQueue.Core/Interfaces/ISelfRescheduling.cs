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

namespace NoMercyQueue.Core.Interfaces;

/// <summary>
/// Optional companion to <see cref="IShouldQueue"/> for a long-running
/// coordinator that wakes repeatedly on one queue row. A job that rewrote its
/// own row in place during <see cref="IShouldQueue.Handle"/> reports
/// <see cref="RescheduledInPlace"/> true, and the worker leaves that row alone
/// instead of deleting it on the way out.
///
/// <para>The alternative — enqueue a successor and let the worker delete the
/// original — hands the coordinator a brand new autoincrement ID on every
/// wake-up. That ID is the encode's identity everywhere downstream: the
/// dashboard prints it, list keys are built from it, and the queue is ordered
/// by the row behind it. A number that changes every thirty seconds cannot
/// carry any of that, and each place that leaned on it broke separately.</para>
/// </summary>
public interface ISelfRescheduling
{
    bool RescheduledInPlace { get; }
}
