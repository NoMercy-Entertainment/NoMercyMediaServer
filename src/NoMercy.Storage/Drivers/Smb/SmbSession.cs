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

using SMBLibrary;
using SMBLibrary.Client;

namespace NoMercy.Storage.Drivers.Smb;

/// <summary>
/// One live SMB connection: a connected+logged-in client with a tree-connected
/// share. Short metadata operations use a session for the duration of a single
/// call; read/write streams own a session for the whole transfer. Disposing
/// tears the connection down.
/// </summary>
internal sealed class SmbSession : IDisposable
{
    public required SMB2Client Client { get; init; }
    public required ISMBFileStore Store { get; init; }

    public void Dispose()
    {
        try
        {
            Store.Disconnect();
        }
        catch
        {
            // ignore
        }
        try
        {
            Client.Logoff();
        }
        catch
        {
            // ignore
        }
        try
        {
            Client.Disconnect();
        }
        catch
        {
            // ignore
        }
    }
}

/// <summary>SMB2 status-to-exception helper shared by the driver and its streams.</summary>
internal static class SmbStatus
{
    public static void EnsureSuccess(NTStatus status, string what)
    {
        if (status != NTStatus.STATUS_SUCCESS)
            throw new IOException(message: $"SMB {what} failed (status {status}).");
    }
}
