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

using NoMercy.Api.Controllers.V1.Dashboard.Media;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// ConfirmDisc used to accept any client-supplied absolute RipOutputPath and then
/// copy+delete it. These assert the confinement guard rejects everything outside
/// the server's own rip staging directory.
/// </summary>
public class RipStagingPathTests
{
    private static readonly string StagingRoot = Path.Combine(
        path1: Path.GetTempPath(),
        path2: "nm-ripstaging-test",
        path3: "ripper"
    );

    [Fact]
    public void FileDirectlyInsideStaging_IsAllowed()
    {
        string candidate = Path.Combine(path1: StagingRoot, path2: "drive0", path3: "movie.mkv");
        Assert.True(condition: RipStagingPath.IsWithinStaging(ripOutputPath: candidate, stagingRoot: StagingRoot));
    }

    [Fact]
    public void TraversalOutOfStaging_IsRejected()
    {
        string candidate = Path.Combine(path1: StagingRoot, path2: "..", path3: "..", path4: "secret.conf");
        Assert.False(condition: RipStagingPath.IsWithinStaging(ripOutputPath: candidate, stagingRoot: StagingRoot));
    }

    [Fact]
    public void SiblingPrefixDirectory_IsRejected()
    {
        // "ripper-evil" must not be treated as inside "ripper".
        string candidate = Path.Combine(path1: StagingRoot + "-evil", path2: "movie.mkv");
        Assert.False(condition: RipStagingPath.IsWithinStaging(ripOutputPath: candidate, stagingRoot: StagingRoot));
    }

    [Fact]
    public void StagingRootItself_IsRejected()
    {
        Assert.False(condition: RipStagingPath.IsWithinStaging(ripOutputPath: StagingRoot, stagingRoot: StagingRoot));
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "   ")]
    public void EmptyOrNullPath_IsRejected(string? candidate)
    {
        Assert.False(condition: RipStagingPath.IsWithinStaging(ripOutputPath: candidate, stagingRoot: StagingRoot));
    }

    [Fact]
    public void AbsolutePathOutsideStaging_IsRejected()
    {
        string candidate = Path.Combine(path1: Path.GetTempPath(), path2: "elsewhere", path3: "movie.mkv");
        Assert.False(condition: RipStagingPath.IsWithinStaging(ripOutputPath: candidate, stagingRoot: StagingRoot));
    }
}
