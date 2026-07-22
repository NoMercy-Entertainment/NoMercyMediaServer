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

using NoMercy.Monitoring;

namespace NoMercy.Tests.Monitoring;

/// <summary>
/// A test double for the internal <see cref="IResourceProvider"/> contract
/// (reachable here via <c>InternalsVisibleTo</c>) that always throws. Neither
/// real provider realistically throws out of <c>Collect()</c> — both wrap every
/// OS-facing read in their own try/catch — so this is how
/// <see cref="ResourceMonitor.Monitor"/>'s own defensive catch is exercised:
/// with a real implementation of the contract it depends on, not a mock of the
/// OS underneath it.
/// </summary>
internal sealed class ThrowingResourceProvider : IResourceProvider
{
    public Resource Collect() => throw new InvalidOperationException(message: "Simulated provider failure.");
}
