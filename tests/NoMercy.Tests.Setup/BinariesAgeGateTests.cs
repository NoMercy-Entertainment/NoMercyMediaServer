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

using NoMercy.Setup.Dto;
using NoMercy.Setup.Server;

namespace NoMercy.Tests.Setup;

[Trait(name: "Category", value: "Unit")]
public class BinariesAgeGateTests
{
    private static readonly DateTimeOffset Now = new(year: 2026, month: 07, day: 08, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = Now - TimeSpan.FromDays(days: 14);

    private static GithubReleaseResponse Release(
        int daysOld,
        bool draft = false,
        bool prerelease = false
    ) =>
        new()
        {
            PublishedAt = Now - TimeSpan.FromDays(days: daysOld),
            Draft = draft,
            Prerelease = prerelease,
        };

    [Fact]
    public void SelectNewestPublishedBefore_PicksNewestOldEnoughRelease()
    {
        GithubReleaseResponse[] releases =
        [
            Release(daysOld: 2), // too new — inside the 14-day gate
            Release(daysOld: 20), // eligible, newest of the eligible set
            Release(daysOld: 40), // eligible but older
        ];

        GithubReleaseResponse? picked = Binaries.SelectNewestPublishedBefore(releases: releases, cutoff: Cutoff);

        picked.Should().NotBeNull();
        picked!.PublishedAt.Should().Be(expected: Now - TimeSpan.FromDays(days: 20));
    }

    [Fact]
    public void SelectNewestPublishedBefore_AllTooNew_ReturnsNull()
    {
        GithubReleaseResponse[] releases = [Release(daysOld: 1), Release(daysOld: 5), Release(daysOld: 13)];

        Binaries.SelectNewestPublishedBefore(releases: releases, cutoff: Cutoff).Should().BeNull();
    }

    [Fact]
    public void SelectNewestPublishedBefore_SkipsDraftsAndPrereleases()
    {
        GithubReleaseResponse[] releases =
        [
            Release(daysOld: 20, draft: true),
            Release(daysOld: 25, prerelease: true),
            Release(daysOld: 30), // the only eligible stable release
        ];

        GithubReleaseResponse? picked = Binaries.SelectNewestPublishedBefore(releases: releases, cutoff: Cutoff);

        picked.Should().NotBeNull();
        picked!.PublishedAt.Should().Be(expected: Now - TimeSpan.FromDays(days: 30));
    }

    [Fact]
    public void SelectNewestPublishedBefore_IgnoresReleasesWithNoPublishDate()
    {
        GithubReleaseResponse[] releases =
        [
            new() { PublishedAt = DateTimeOffset.MinValue },
            Release(daysOld: 30),
        ];

        GithubReleaseResponse? picked = Binaries.SelectNewestPublishedBefore(releases: releases, cutoff: Cutoff);

        picked.Should().NotBeNull();
        picked!.PublishedAt.Should().Be(expected: Now - TimeSpan.FromDays(days: 30));
    }
}
