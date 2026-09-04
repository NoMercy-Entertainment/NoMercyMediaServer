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

using NoMercy.Encoder.Commands;

namespace NoMercy.Tests.Encoder.Commands;

public class ThumbnailFilterResolverTests
{
    private const string Tonemap =
        "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,"
        + "tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";

    [Fact]
    public void Resolve_SdrSource_NoTonemap()
    {
        string filter = ThumbnailFilterResolver.Resolve(
            intervalSeconds: 10,
            width: 320,
            sourceIsHdr: false,
            tonemapChain: Tonemap
        );

        filter.Should().Be("fps=1/10,scale=320:-2,format=yuvj420p");
        filter.Should().NotContain("tonemap");
    }

    [Fact]
    public void Resolve_HdrSource_PrependsTonemap()
    {
        string filter = ThumbnailFilterResolver.Resolve(
            intervalSeconds: 10,
            width: 320,
            sourceIsHdr: true,
            tonemapChain: Tonemap
        );

        filter.Should().Be($"fps=1/10,{Tonemap},scale=320:-2,format=yuvj420p");
        filter.Should().Contain("tonemap=hable", "HDR sprites must be tonemapped to SDR");
    }

    [Fact]
    public void Resolve_HdrSource_NullChain_FallsBackToDefaultTonemap()
    {
        // No video branch supplied a chain (e.g. all-HDR-preserve ladder) but the
        // sprite still must be SDR — use the built-in hable chain.
        string filter = ThumbnailFilterResolver.Resolve(
            intervalSeconds: 5,
            width: 240,
            sourceIsHdr: true,
            tonemapChain: null
        );

        filter.Should().Contain("tonemap=hable");
        filter.Should().StartWith("fps=1/5,");
        filter.Should().EndWith(",scale=240:-2,format=yuvj420p");
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, 249)]
    [InlineData(true, 249)]
    public void Resolve_DecimatesBeforeAnyPerFrameWork(bool sourceIsHdr, int? padToCells)
    {
        // A 41-minute episode decodes ~59 600 frames to keep 249. Anything placed
        // ahead of fps is paid for on all 59 600 — measured at 1178s of CPU for a
        // leading format convert against 787s once fps leads.
        string filter = ThumbnailFilterResolver.Resolve(
            intervalSeconds: 10,
            width: 320,
            sourceIsHdr: sourceIsHdr,
            tonemapChain: Tonemap,
            padToCells: padToCells
        );

        filter.Should().StartWith("fps=1/10,");

        int fpsAt = filter.IndexOf("fps=", StringComparison.Ordinal);

        foreach (string expensive in new[] { "format=", "scale=", "zscale=", "tonemap=", "tpad=" })
        {
            int at = filter.IndexOf(expensive, StringComparison.Ordinal);
            if (at < 0)
                continue;

            at.Should()
                .BeGreaterThan(
                    fpsAt,
                    $"'{expensive}' must run on the kept frames, not on every decoded one"
                );
        }
    }
}
