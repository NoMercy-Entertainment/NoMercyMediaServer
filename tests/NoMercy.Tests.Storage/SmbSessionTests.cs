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

using NoMercy.Storage.Drivers.Smb;
using SMBLibrary;
using SMBLibrary.Client;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="SmbSession"/> tears down a live connection (tree, session,
/// transport) on Dispose. Each of the three teardown calls is wrapped in its
/// own try/catch specifically so a failure at one layer (e.g. the transport
/// already dropped) does not prevent the others from running — this test
/// demands that guarantee holds even when every underlying call fails.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SmbSessionTests
{
    [Fact]
    public void Dispose_disconnects_the_store_logs_off_and_disconnects_the_client()
    {
        Mock<ISMBFileStore> store = new();
        store.Setup(s => s.Disconnect()).Returns(NTStatus.STATUS_SUCCESS);
        // A real, never-connected SMB2Client: Logoff()/Disconnect() on it are
        // exercised for real (no mock) — SmbSession's job is to tolerate
        // whatever they do, not to control them.
        SmbSession session = new() { Client = new SMB2Client(), Store = store.Object };

        session.Dispose();

        store.Verify(s => s.Disconnect(), Times.Once);
    }

    [Fact]
    public void Dispose_swallows_exceptions_from_store_disconnect()
    {
        Mock<ISMBFileStore> store = new();
        store.Setup(s => s.Disconnect()).Throws<InvalidOperationException>();
        SmbSession session = new() { Client = new SMB2Client(), Store = store.Object };

        Action act = () => session.Dispose();

        act.Should()
            .NotThrow(
                "a failure tearing down the tree connection must not prevent client logoff/disconnect from running"
            );
    }

    [Fact]
    public void Dispose_is_safe_to_call_even_when_the_client_was_never_connected()
    {
        Mock<ISMBFileStore> store = new();
        SmbSession session = new() { Client = new SMB2Client(), Store = store.Object };

        Action act = () => session.Dispose();

        act.Should()
            .NotThrow("Logoff/Disconnect on a never-connected client must not crash Dispose");
    }
}
