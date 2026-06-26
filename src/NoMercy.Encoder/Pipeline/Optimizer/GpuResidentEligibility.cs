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

using NoMercy.Encoder.Output;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Pipeline.Optimizer;

/// <summary>
/// Decides whether a plan's video graph can stay fully GPU-resident
/// (decode → GPU scale → encode). Any CPU-only filter — HDR→SDR tonemap (the
/// current zscale path), crop, or subtitle burn-in — forces a hwdownload/upload
/// bounce that usually costs more than the offload saves, so those plans keep the
/// CPU filter graph. Conservative by design: GPU tonemap (libplacebo) and GPU
/// crop are future enhancements that can relax these checks.
/// </summary>
public static class GpuResidentEligibility
{
    public static bool IsEligible(
        IReadOnlyList<VideoOutputPlan> videoOutputs,
        IReadOnlyList<SubtitleOutputPlan> subtitleOutputs
    )
    {
        if (videoOutputs.Count == 0)
            return false;

        foreach (VideoOutputPlan video in videoOutputs)
        {
            if (video.ConvertHdrToSdr)
                return false;
            if (!string.IsNullOrWhiteSpace(video.CropFilter))
                return false;
        }

        foreach (SubtitleOutputPlan subtitle in subtitleOutputs)
        {
            if (subtitle.Policy == SubtitlePolicy.BurnIn)
                return false;
        }

        return true;
    }
}
