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

using NoMercy.App.EmbeddedStaticAssets;
using Xunit;

namespace NoMercy.Tests.App.EmbeddedStaticAssets;

/// <summary>
/// REQUIREMENT: out of the box (no configuration action supplied), the
/// middleware must inject nothing and must only ever consider injecting into
/// <c>index.html</c> — every other embedded asset must be served byte-for-byte
/// untouched. <see cref="EmbeddedStaticAssetsMiddlewareTests"/> exercises the
/// behavior these defaults drive; this file pins the defaults themselves.
/// </summary>
public sealed class EmbeddedStaticAssetsOptionsTests
{
    [Fact]
    public void Defaults_InjectNothing()
    {
        EmbeddedStaticAssetsOptions options = new();

        options.InjectScripts.Should().BeEmpty();
        options.InjectStyles.Should().BeEmpty();
        options.InjectMetaTags.Should().BeEmpty();
    }

    [Fact]
    public void Defaults_OnlyMatchIndexHtml()
    {
        EmbeddedStaticAssetsOptions options = new();

        options.HtmlFilePatterns.Should().ContainSingle().Which.Should().Be("index.html");
    }

    [Fact]
    public void Defaults_MinifyInjectionsIsEnabled()
    {
        EmbeddedStaticAssetsOptions options = new();

        options.MinifyInjections.Should().BeTrue();
    }
}
