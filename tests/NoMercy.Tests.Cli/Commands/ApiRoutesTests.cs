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

using NoMercy.Cli.Commands;
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: every CLI command talks to the server's management surface
/// under the fixed <c>/manage</c> prefix at these exact paths. The management
/// HTTP endpoints (server-side) are a separate deployable from the CLI, so a
/// silent rename on either side breaks every installed CLI until both are
/// upgraded together — locking the literal values in guards against that.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ApiRoutesTests
{
    [Fact]
    public void Routes_MatchDocumentedManagementPaths()
    {
        ApiRoutes.Update.Should().Be("/manage/update");
        ApiRoutes.Stop.Should().Be("/manage/stop");
        ApiRoutes.Restart.Should().Be("/manage/restart");
        ApiRoutes.Config.Should().Be("/manage/config");
        ApiRoutes.Plugins.Should().Be("/manage/plugins");
        ApiRoutes.Status.Should().Be("/manage/status");
        ApiRoutes.Resources.Should().Be("/manage/resources");
        ApiRoutes.Queue.Should().Be("/manage/queue");
        ApiRoutes.AutoStart.Should().Be("/manage/autostart");
        ApiRoutes.Logs.Should().Be("/manage/logs");
        ApiRoutes.LogsStream.Should().Be("/manage/logs/stream");
    }
}
