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

using NoMercy.Networking.Discovery;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: constructing an IpcClient (default pipe/socket name, or an
/// explicit override) must never itself attempt a connection — the actual
/// named-pipe (Windows) or Unix-domain-socket (Linux/macOS) connect only
/// happens lazily on the first request, inside the ConnectCallback. That
/// callback — and every GetAsync/PostAsync/PutAsync/SendAsync/GetStreamAsync
/// call that would trigger it — requires a real management IPC server
/// listening on the far end and is itemized as not unit-testable — see the
/// coverage report.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class IpcClientTests
{
    [Fact]
    public void Constructor_DefaultPipeName_DoesNotThrow_AndDoesNotConnect()
    {
        Exception? ex = Record.Exception(testCode: () =>
        {
            using IpcClient client = new();
        });

        Assert.Null(@object: ex);
    }

    [Fact]
    public void Constructor_ExplicitPipeName_DoesNotThrow()
    {
        Exception? ex = Record.Exception(testCode: () =>
        {
            using IpcClient client = new(pipeNameOrSocketPath: "nomercy-test-pipe");
        });

        Assert.Null(@object: ex);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        IpcClient client = new();

        Exception? ex = Record.Exception(testCode: () =>
        {
            client.Dispose();
            client.Dispose();
        });

        Assert.Null(@object: ex);
    }
}
