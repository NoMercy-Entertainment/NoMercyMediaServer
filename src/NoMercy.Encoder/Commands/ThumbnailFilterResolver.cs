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

using NoMercy.Encoder.PostProcess;

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
    ) => Resolve(intervalSeconds, width, sourceIsHdr, tonemapChain, padToCells: null);

    /// <summary>
    /// <paramref name="padToCells"/> appends that many black frames to the end of
    /// the sampled stream. Paired with a cut at the same count and a stated
    /// column count, it leaves the sheet's grid exactly full — which is the only
    /// way to keep the leftover cells from coming out green. See
    /// <see cref="SpriteGrid"/>. Null leaves the stream alone.
    /// </summary>
    public static string Resolve(
        int intervalSeconds,
        int width,
        bool sourceIsHdr,
        string? tonemapChain,
        int? padToCells
    )
    {
        // fps leads, and everything expensive follows it. A 41-minute episode
        // decodes ~59 600 frames to keep 249; running the pixel-format convert,
        // the tonemap and the scale ahead of that decimation paid for all 59 600.
        string filter = $"fps=1/{intervalSeconds}";

        if (sourceIsHdr)
            filter +=
                $",{(string.IsNullOrEmpty(tonemapChain) ? DefaultTonemapChain : tonemapChain)}";

        filter += $",scale={width}:-2,format=yuvj420p";

        if (padToCells is > 0)
            filter += $",tpad=stop={padToCells}:stop_mode=add:color=black";

        return filter;
    }
}
