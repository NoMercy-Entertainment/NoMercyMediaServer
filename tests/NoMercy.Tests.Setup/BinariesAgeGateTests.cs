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

[Trait("Category", "Unit")]
public class BinariesAgeGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 08, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = Now - TimeSpan.FromDays(14);

    private static GithubReleaseResponse Release(
        int daysOld,
        bool draft = false,
        bool prerelease = false
    ) =>
        new()
        {
            PublishedAt = Now - TimeSpan.FromDays(daysOld),
            Draft = draft,
            Prerelease = prerelease,
        };

    [Fact]
    public void SelectNewestPublishedBefore_PicksNewestOldEnoughRelease()
    {
        GithubReleaseResponse[] releases =
        [
            Release(2), // too new — inside the 14-day gate
            Release(20), // eligible, newest of the eligible set
            Release(40), // eligible but older
        ];

        GithubReleaseResponse? picked = Binaries.SelectNewestPublishedBefore(releases, Cutoff);

        picked.Should().NotBeNull();
        picked!.PublishedAt.Should().Be(Now - TimeSpan.FromDays(20));
    }

    [Fact]
    public void SelectNewestPublishedBefore_AllTooNew_ReturnsNull()
    {
        GithubReleaseResponse[] releases = [Release(1), Release(5), Release(13)];

        Binaries.SelectNewestPublishedBefore(releases, Cutoff).Should().BeNull();
    }

    [Fact]
    public void SelectNewestPublishedBefore_SkipsDraftsAndPrereleases()
    {
        GithubReleaseResponse[] releases =
        [
            Release(20, draft: true),
            Release(25, prerelease: true),
            Release(30), // the only eligible stable release
        ];

        GithubReleaseResponse? picked = Binaries.SelectNewestPublishedBefore(releases, Cutoff);

        picked.Should().NotBeNull();
        picked!.PublishedAt.Should().Be(Now - TimeSpan.FromDays(30));
    }

    [Fact]
    public void SelectNewestPublishedBefore_IgnoresReleasesWithNoPublishDate()
    {
        GithubReleaseResponse[] releases =
        [
            new() { PublishedAt = DateTimeOffset.MinValue },
            Release(30),
        ];

        GithubReleaseResponse? picked = Binaries.SelectNewestPublishedBefore(releases, Cutoff);

        picked.Should().NotBeNull();
        picked!.PublishedAt.Should().Be(Now - TimeSpan.FromDays(30));
    }
}
