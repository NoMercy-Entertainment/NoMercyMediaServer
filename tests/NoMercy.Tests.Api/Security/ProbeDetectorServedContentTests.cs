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

using NoMercy.Api.Security;
using Xunit;

namespace NoMercy.Tests.Api.Security;

/// <summary>
/// A request this server answered with content is not a probe. The exploit-fragment
/// list contains strings that appear in assets the server itself serves — Swagger's
/// bundle being the one that banned a real operator — so a successful, routed response
/// must clear before those substrings are consulted.
/// </summary>
[Trait("Category", "Unit")]
public class ProbeDetectorServedContentTests
{
    [Theory]
    [InlineData("/swagger-ui-bundle.js")]
    [InlineData("/swagger-ui-standalone-preset.js")]
    [InlineData("/swagger/swagger-ui-bundle.js")]
    [InlineData("/swagger/swagger-ui-standalone-preset.js")]
    public void ServedSwaggerAsset_IsNotAProbe(string path)
    {
        RequestOutcome outcome = new(
            path,
            EndpointMatched: true,
            StatusCode: 200,
            IsAuthenticated: false
        );

        Assert.Equal(ProbeVerdict.Clean, ProbeDetector.Classify(outcome));
    }

    [Theory]
    [InlineData("/.env")]
    [InlineData("/wp-login.php")]
    [InlineData("/vendor/phpunit/phpunit/phpunit.php")]
    [InlineData("/actuator/health")]
    public void UnservedExploitPath_IsStillAKnownProbe(string path)
    {
        RequestOutcome outcome = new(
            path,
            EndpointMatched: false,
            StatusCode: 404,
            IsAuthenticated: false
        );

        Assert.Equal(ProbeVerdict.KnownProbe, ProbeDetector.Classify(outcome));
    }

    /// <summary>
    /// A fragment with no boundary matches ordinary library paths. "/aws" did, and
    /// "/.aws/" already covers the credentials directory it was meant to catch. Such a
    /// path may still score as an ordinary unrouted miss; what it must not do is carry
    /// the known-probe weight, where two hits are a permanent ban.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/movie/awsome-title")]
    [InlineData("/media/movies/Awsome%20Movie/poster.jpg")]
    public void LibraryPathResemblingACredentialFragment_DoesNotCarryProbeWeight(string path)
    {
        RequestOutcome outcome = new(
            path,
            EndpointMatched: false,
            StatusCode: 404,
            IsAuthenticated: false
        );

        ProbeVerdict verdict = ProbeDetector.Classify(outcome);

        Assert.NotEqual(ProbeVerdict.KnownProbe, verdict);
        Assert.True(ProbeDetector.Weight(verdict) <= 1);
    }

    [Fact]
    public void AwsCredentialsDirectory_IsStillAKnownProbe()
    {
        RequestOutcome outcome = new(
            "/.aws/credentials",
            EndpointMatched: false,
            StatusCode: 404,
            IsAuthenticated: false
        );

        Assert.Equal(ProbeVerdict.KnownProbe, ProbeDetector.Classify(outcome));
    }

    [Fact]
    public void ExploitPathThatSomehowRoutesButFails_IsStillAKnownProbe()
    {
        RequestOutcome outcome = new(
            "/.git/config",
            EndpointMatched: true,
            StatusCode: 403,
            IsAuthenticated: false
        );

        Assert.Equal(ProbeVerdict.KnownProbe, ProbeDetector.Classify(outcome));
    }
}
