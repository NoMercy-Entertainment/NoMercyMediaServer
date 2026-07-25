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

using NoMercy.OpticalMedia.Live;

namespace NoMercy.Tests.OpticalMedia.Live;

/// <summary>
/// REQUIREMENT: <see cref="DiscSessionRegistry"/> maps a drive path to the
/// live HLS session id currently streaming from it, so
/// <c>OpticalMediaController.StopMedia</c> can tear down the right session.
/// Registration must be overwritable, lookup must fail cleanly for an
/// unregistered drive, and removal must be idempotent.
/// </summary>
[Trait("Category", "Unit")]
public class DiscSessionRegistryTests
{
    [Fact]
    public void TryGet_UnregisteredDrive_ReturnsFalse()
    {
        DiscSessionRegistry registry = new();

        bool found = registry.TryGet("D:\\", out string sessionId);

        found.Should().BeFalse();
        sessionId.Should().BeNull();
    }

    [Fact]
    public void Register_ThenTryGet_ReturnsSessionId()
    {
        DiscSessionRegistry registry = new();

        registry.Register("D:\\", "session-123");
        bool found = registry.TryGet("D:\\", out string sessionId);

        found.Should().BeTrue();
        sessionId.Should().Be("session-123");
    }

    [Fact]
    public void Register_CalledTwiceForSameDrive_OverwritesSessionId()
    {
        DiscSessionRegistry registry = new();

        registry.Register("D:\\", "session-old");
        registry.Register("D:\\", "session-new");
        registry.TryGet("D:\\", out string sessionId);

        sessionId.Should().Be("session-new");
    }

    [Fact]
    public void Remove_RegisteredDrive_ClearsMapping()
    {
        DiscSessionRegistry registry = new();
        registry.Register("D:\\", "session-123");

        registry.Remove("D:\\");
        bool found = registry.TryGet("D:\\", out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void Remove_UnregisteredDrive_DoesNotThrow()
    {
        DiscSessionRegistry registry = new();

        Action act = () => registry.Remove("D:\\");

        act.Should().NotThrow();
    }

    [Fact]
    public void Register_IsCaseInsensitiveOnDrivePath()
    {
        DiscSessionRegistry registry = new();

        registry.Register("D:\\", "session-123");
        bool found = registry.TryGet("d:\\", out string sessionId);

        found.Should().BeTrue();
        sessionId.Should().Be("session-123");
    }
}
