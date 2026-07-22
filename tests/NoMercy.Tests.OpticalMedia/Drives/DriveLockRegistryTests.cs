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

using NoMercy.OpticalMedia.Drives;

namespace NoMercy.Tests.OpticalMedia.Drives;

/// <summary>
/// REQUIREMENT: <see cref="DriveLockRegistry"/> must guarantee one active rip
/// per physical drive — a second rip request for a drive that is already
/// locked must be rejected, and releasing the lock (via <see cref="IDisposable.Dispose"/>)
/// must free the drive key for a subsequent rip. Different drive keys must
/// never contend with each other.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class DriveLockRegistryTests
{
    [Fact]
    public void TryAcquire_FirstCallForKey_Succeeds()
    {
        DriveLockRegistry registry = new();

        bool acquired = registry.TryAcquire(driveKey: "D:\\", driveLock: out DriveLock? driveLock);

        acquired.Should().BeTrue();
        driveLock.Should().NotBeNull();
    }

    [Fact]
    public void TryAcquire_SecondCallForSameKey_Fails()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire(driveKey: "D:\\", driveLock: out _);

        bool acquiredAgain = registry.TryAcquire(driveKey: "D:\\", driveLock: out DriveLock? driveLock);

        acquiredAgain.Should().BeFalse();
        driveLock.Should().BeNull();
    }

    [Fact]
    public void TryAcquire_DifferentKeys_BothSucceed()
    {
        DriveLockRegistry registry = new();

        bool first = registry.TryAcquire(driveKey: "D:\\", driveLock: out _);
        bool second = registry.TryAcquire(driveKey: "E:\\", driveLock: out _);

        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_IsCaseInsensitive()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire(driveKey: "volume-uuid-ABC", driveLock: out _);

        bool acquiredLower = registry.TryAcquire(driveKey: "volume-uuid-abc", driveLock: out DriveLock? driveLock);

        acquiredLower.Should().BeFalse();
        driveLock.Should().BeNull();
    }

    [Fact]
    public void Dispose_ReleasesLock_AllowingReacquisition()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire(driveKey: "D:\\", driveLock: out DriveLock? driveLock);

        driveLock!.Dispose();
        bool reacquired = registry.TryAcquire(driveKey: "D:\\", driveLock: out DriveLock? second);

        reacquired.Should().BeTrue();
        second.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_CalledTwice_IsIdempotent()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire(driveKey: "D:\\", driveLock: out DriveLock? driveLock);

        driveLock!.Dispose();
        Action secondDispose = () => driveLock.Dispose();

        secondDispose.Should().NotThrow();
        registry
            .TryAcquire(driveKey: "D:\\", driveLock: out _)
            .Should()
            .BeTrue(because: "first Dispose already released the lock");
    }

    [Fact]
    public void Dispose_OnOneKey_DoesNotReleaseOtherKeys()
    {
        DriveLockRegistry registry = new();
        registry.TryAcquire(driveKey: "D:\\", driveLock: out DriveLock? driveLockD);
        registry.TryAcquire(driveKey: "E:\\", driveLock: out _);

        driveLockD!.Dispose();

        registry.TryAcquire(driveKey: "D:\\", driveLock: out _).Should().BeTrue();
        registry.TryAcquire(driveKey: "E:\\", driveLock: out _).Should().BeFalse(because: "E:\\ was never released");
    }
}
