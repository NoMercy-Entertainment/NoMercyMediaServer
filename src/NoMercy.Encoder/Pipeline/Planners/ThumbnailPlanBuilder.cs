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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Thumbnail/sprite plan selection extracted from PlanStage.BuildOutputPlanAsync.
/// Pure projection over the profile + probed media; no encoder state.
/// </summary>
public static class ThumbnailPlanBuilder
{
    public static ThumbnailOutputPlan? Build(EncodingProfile profile, MediaInfo media)
    {
        ThumbnailOutputPlan? thumbPlan = null;
        if (media.VideoStreams.Count > 0)
        {
            // Explicit profile.Thumbnails wins. Otherwise fall back to
            // HlsDerivatives: when GenerateSpriteVtt is on (the default for HLS),
            // build a ThumbnailOutput from SpriteVttThumbnailWidth +
            // SpriteVttIntervalSeconds so sprites land for every HLS preset
            // without each author having to also set the legacy Thumbnails field.
            // Null HlsDerivatives (V1-backfilled presets) gets treated as a
            // fresh HlsDerivatives() so the sprite-on defaults still apply.
            ThumbnailOutput? thumbConfig = profile.Thumbnails;
            if (thumbConfig is null)
            {
                HlsDerivatives derivatives = profile.HlsDerivatives ?? new HlsDerivatives();
                if (derivatives.GenerateSpriteVtt)
                {
                    thumbConfig = new ThumbnailOutput(
                        Width: derivatives.SpriteVttThumbnailWidth,
                        IntervalSeconds: derivatives.SpriteVttIntervalSeconds
                    );
                }
            }

            if (thumbConfig is not null)
            {
                int thumbHeight = (int)(
                    2
                    * Math.Round(
                        (double)thumbConfig.Width
                            * media.VideoStreams[0].Height
                            / media.VideoStreams[0].Width
                            / 2
                    )
                );

                thumbPlan = new(thumbConfig.Width, thumbHeight, thumbConfig.IntervalSeconds);
            }
        }
        return thumbPlan;
    }
}
