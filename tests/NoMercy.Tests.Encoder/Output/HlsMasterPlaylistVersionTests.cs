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

using NoMercy.Encoder.Output;

namespace NoMercy.Tests.Encoder.Output;

public class HlsMasterPlaylistVersionTests
{
    [Fact]
    public void ComputeMasterVersion_returns_3_for_basic_mpegts()
    {
        int version = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: false,
            hasFmp4: false,
            hasChapterDateRanges: false
        );

        Assert.Equal(expected: 3, actual: version);
    }

    [Fact]
    public void ComputeMasterVersion_returns_6_when_subs_group_present()
    {
        int version = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: true,
            hasFmp4: false,
            hasChapterDateRanges: false
        );

        Assert.Equal(expected: 6, actual: version);
    }

    [Fact]
    public void ComputeMasterVersion_returns_7_when_fmp4()
    {
        int version = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: false,
            hasFmp4: true,
            hasChapterDateRanges: false
        );

        Assert.Equal(expected: 7, actual: version);
    }

    [Fact]
    public void ComputeMasterVersion_returns_8_when_chapter_dateranges()
    {
        int version = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: false,
            hasFmp4: false,
            hasChapterDateRanges: true
        );

        Assert.Equal(expected: 8, actual: version);
    }

    [Fact]
    public void ComputeMasterVersion_picks_highest_when_multiple_features()
    {
        // subs + fmp4 → 7
        int versionSubsFmp4 = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: true,
            hasFmp4: true,
            hasChapterDateRanges: false
        );

        Assert.Equal(expected: 7, actual: versionSubsFmp4);

        // subs + fmp4 + chapters → 8
        int versionAll = PlaylistGenerator.ComputeMasterVersion(
            hasSubsGroup: true,
            hasFmp4: true,
            hasChapterDateRanges: true
        );

        Assert.Equal(expected: 8, actual: versionAll);
    }
}
