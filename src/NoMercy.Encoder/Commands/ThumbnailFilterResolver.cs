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

namespace NoMercy.Encoder.Commands;

/// <summary>
/// Builds the thumbnail/sprite source filter. On HDR sources the sprite must be
/// tonemapped to SDR or it shows crushed colours; the dedupe path already routes
/// from the shared [sdr] intermediate, but the single-branch / non-dedupe paths
/// sampled raw HDR. This resolver is the one place that decision lives.
/// </summary>
public static class ThumbnailFilterResolver
{
    // Mirrors FilterGraphBuilder.AddTonemap's CPU chain so an all-HDR-preserve
    // ladder (no video branch chain to borrow) still produces SDR sprites.
    private const string DefaultTonemapChain =
        "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,"
        + "tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";

    public static string Resolve(
        int intervalSeconds,
        int width,
        bool sourceIsHdr,
        string? tonemapChain
    )
    {
        string baseFilter = $"format=yuvj420p,fps=1/{intervalSeconds},scale={width}:-2";
        if (!sourceIsHdr)
            return baseFilter;

        string chain = string.IsNullOrEmpty(value: tonemapChain) ? DefaultTonemapChain : tonemapChain;
        return $"{chain},{baseFilter}";
    }
}
