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

using NoMercy.Cli;
using Xunit;

namespace NoMercy.Tests.Cli;

/// <summary>
/// REQUIREMENT (see <see cref="ICliClientFactory"/> doc comment): the factory
/// must hand out a fresh client per call — the pipe/socket path is only known at
/// command-invocation time, so nothing may cache or share a single instance
/// across commands.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CliClientFactoryTests
{
    [Fact]
    public void Create_ReturnsNonNullClient()
    {
        CliClientFactory factory = new();

        using ICliClient client = factory.Create(null);

        client.Should().NotBeNull();
    }

    [Fact]
    public void Create_ReturnsDistinctInstance_OnEachCall()
    {
        CliClientFactory factory = new();

        using ICliClient first = factory.Create("nomercy-test-pipe-a");
        using ICliClient second = factory.Create("nomercy-test-pipe-b");

        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void Create_AcceptsNullPipeName_WithoutThrowing()
    {
        CliClientFactory factory = new();

        Exception? ex = Record.Exception(() =>
        {
            using ICliClient client = factory.Create(null);
        });

        ex.Should().BeNull();
    }
}
