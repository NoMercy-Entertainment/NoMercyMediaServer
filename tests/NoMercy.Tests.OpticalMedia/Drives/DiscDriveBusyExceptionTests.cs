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

using NoMercy.Encoder.Errors;
using NoMercy.OpticalMedia.Drives;

namespace NoMercy.Tests.OpticalMedia.Drives;

/// <summary>
/// REQUIREMENT: <see cref="DiscDriveBusyException"/> must carry the busy
/// drive's lock key and a stable rule id so API controllers can surface a
/// 409 with a dashboard-linkable rule, and the message must name the drive.
/// </summary>
[Trait("Category", "Unit")]
public class DiscDriveBusyExceptionTests
{
    [Fact]
    public void Constructor_SetsDriveKeyProperty()
    {
        DiscDriveBusyException ex = new("D:\\");

        ex.DriveKey.Should().Be("D:\\");
    }

    [Fact]
    public void Constructor_SetsStableRuleId()
    {
        DiscDriveBusyException ex = new("D:\\");

        ex.RuleId.Should().Be(EncoderRuleId.DiscDriveBusy);
    }

    [Fact]
    public void Message_MentionsDriveKey()
    {
        DiscDriveBusyException ex = new("volume-uuid-123");

        ex.Message.Should().Contain("volume-uuid-123");
    }

    [Fact]
    public void Message_ExplainsInProgressRip()
    {
        DiscDriveBusyException ex = new("D:\\");

        ex.Message.Should().Contain("already being ripped");
    }

    [Fact]
    public void IsInvalidOperationException()
    {
        DiscDriveBusyException ex = new("D:\\");

        ex.Should().BeAssignableTo<InvalidOperationException>();
    }
}
