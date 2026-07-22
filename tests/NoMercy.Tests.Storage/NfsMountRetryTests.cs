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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Tests.Storage.Faults;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Regression coverage for the rescan-does-nothing bug: a single transient
/// libnfs mount timeout ("command timed out") threw straight out of the driver
/// constructor, so every operation that lazily built the driver — a rescan
/// among them — failed even though the NFS server was healthy and answering.
/// The mount now retries on a fresh context, matching every other libnfs call
/// in the driver.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class NfsMountRetryTests
{
    private static NfsDriverConfig Config() =>
        NfsDriverConfig.For(server: "fake-server", export: "/export", version: 4);

    [Fact]
    public void Mount_succeeds_first_attempt_without_retry()
    {
        FaultyLibNfs fake = new();

        using NfsStorageDriver driver = new(config: Config(), libNfs: fake, log: NullLogger.Instance);

        fake.CallCounts.GetValueOrDefault(key: "Mount").Should().Be(expected: 1);
    }

    [Fact]
    public void Transient_mount_timeout_is_retried_and_succeeds()
    {
        FaultyLibNfs fake = new();
        // First mount RPC times out the way the NAS did mid-encode; the retry
        // (call index 1) lands on a healthy server.
        fake.Faults[key: "Mount:0"] = (-1, "command timed out");

        using NfsStorageDriver driver = new(config: Config(), libNfs: fake, log: NullLogger.Instance);

        fake.CallCounts.GetValueOrDefault(key: "Mount").Should().Be(expected: 2);
        // Each retry rebuilds the libnfs context from scratch.
        fake.CallCounts.GetValueOrDefault(key: "InitContext").Should().Be(expected: 2);
    }

    [Fact]
    public void Mount_that_keeps_timing_out_throws_after_the_attempt_budget()
    {
        FaultyLibNfs fake = new();
        fake.Faults[key: "Mount:0"] = (-1, "command timed out");
        fake.Faults[key: "Mount:1"] = (-1, "command timed out");
        fake.Faults[key: "Mount:2"] = (-1, "command timed out");

        Action act = () =>
        {
            using NfsStorageDriver _ = new(config: Config(), libNfs: fake, log: NullLogger.Instance);
        };

        act.Should()
            .Throw<IOException>()
            .WithMessage(expectedWildcardPattern: "*mount failed*after 3 attempts*command timed out*");
        fake.CallCounts.GetValueOrDefault(key: "Mount").Should().Be(expected: 3);
    }
}
