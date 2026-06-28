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

using FluentAssertions;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Status;
using Xunit;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// Contract coverage for the Slice 03 shared status/auth singletons. These hold
/// process-wide mutable state behind interfaces so the rest of the app does not
/// reach for statics; the behaviour that matters is the state transition and,
/// for the token store, the change notification.
/// </summary>
public class SystemStatusContractTests
{
    [Fact]
    public void AuthTokenStore_SetAccessToken_updates_value_and_raises_event()
    {
        AuthTokenStore store = new();
        string? observed = "<unset>";
        store.AccessTokenChanged += (_, token) => observed = token;

        store.AccessToken.Should().BeNull();

        store.SetAccessToken("abc123");

        store.AccessToken.Should().Be("abc123");
        observed.Should().Be("abc123");
    }

    [Fact]
    public void BootStatus_marks_started_and_stopped()
    {
        BootStatus status = new();

        status.IsStarted.Should().BeFalse();

        status.MarkStarted();
        status.IsStarted.Should().BeTrue();

        status.MarkStopped();
        status.IsStarted.Should().BeFalse();
    }

    [Fact]
    public void ConnectivityStatus_defaults_to_no_nat_and_no_port_forward()
    {
        ConnectivityStatus status = new();

        status.NatStatus.Should().Be(NatStatus.None);
        status.PortForwarded.Should().BeFalse();
    }
}
