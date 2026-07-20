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

using NoMercy.Service;
using Xunit;

namespace NoMercy.Tests.Service;

/// <summary>
/// <see cref="Dev.Run"/> is the dev-only hook <see cref="Hosting.ServerBootstrapper"/>
/// awaits once the host is live. Its body is currently commented out (an ad-hoc
/// diagnostic script, not a shipped feature); the reachable contract is simply
/// "completes without throwing regardless of environment" so a leftover dev
/// hook can never fail a real boot. The private playlist/bitrate helpers below
/// it are unreachable dead code (nothing un-comments them) and are intentionally
/// not exercised here — see the coverage report for that residue.
/// </summary>
[Trait("Category", "Unit")]
public class DevTests
{
    [Fact]
    public async Task Run_CompletesWithoutThrowing()
    {
        Exception? thrown = await Record.ExceptionAsync(() => Dev.Run());

        Assert.Null(thrown);
    }
}
