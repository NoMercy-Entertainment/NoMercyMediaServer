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

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="SmbStatus.EnsureSuccess"/> is the single choke point every SMB
/// operation (connect, tree-connect, create, read, write, close) funnels its
/// NTStatus through. A silently-swallowed non-success status would surface
/// as a corrupted read/write instead of a clear error.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SmbStatusTests
{
    [Fact]
    public void EnsureSuccess_does_not_throw_for_STATUS_SUCCESS()
    {
        Action act = () => SmbStatus.EnsureSuccess(NTStatus.STATUS_SUCCESS, "connect");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(NTStatus.STATUS_ACCESS_DENIED)]
    [InlineData(NTStatus.STATUS_OBJECT_NAME_NOT_FOUND)]
    [InlineData(NTStatus.STATUS_OBJECT_PATH_NOT_FOUND)]
    public void EnsureSuccess_throws_IOException_with_the_operation_name_and_status(NTStatus status)
    {
        Action act = () => SmbStatus.EnsureSuccess(status, "tree connect");

        act.Should().Throw<IOException>().WithMessage($"*tree connect*{status}*");
    }
}
