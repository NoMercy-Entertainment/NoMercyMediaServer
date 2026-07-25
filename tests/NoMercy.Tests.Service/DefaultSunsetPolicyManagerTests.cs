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

using Asp.Versioning;
using NoMercy.Service;

namespace NoMercy.Tests.Service;

/// <summary>
/// NoMercy never sunsets an API version on self-hosted installs — a "yes but
/// no policy configured" real <see cref="ISunsetPolicyManager"/> would make
/// Asp.Versioning start advertising deprecation headers. <see cref="DefaultSunsetPolicyManager"/>
/// is the deliberate no-op: always reports a policy exists, and that policy is
/// the default (no sunset date, no link).
/// </summary>
[Trait("Category", "Unit")]
public class DefaultSunsetPolicyManagerTests
{
    [Fact]
    public void TryGetPolicy_AnyNameAndVersion_ReturnsTrueWithDefaultPolicy()
    {
        DefaultSunsetPolicyManager manager = new();

        bool found = manager.TryGetPolicy("v1", new ApiVersion(1, 0), out SunsetPolicy? policy);

        found.Should().BeTrue();
        policy.Should().NotBeNull();
        SunsetPolicy nonNullPolicy = policy!;
        nonNullPolicy.Date.Should().BeNull();
        nonNullPolicy.HasLinks.Should().BeFalse();
    }

    [Fact]
    public void TryGetPolicy_NullNameAndVersion_StillReturnsTrue()
    {
        DefaultSunsetPolicyManager manager = new();

        bool found = manager.TryGetPolicy(null, null, out SunsetPolicy? policy);

        found.Should().BeTrue();
        policy.Should().NotBeNull();
    }
}
