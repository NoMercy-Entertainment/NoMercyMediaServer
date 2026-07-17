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

        // Sprite generation no longer requires the main video output's own
        // filtergraph: BuildStage routes a copy-video plan's sprite through a
        // SEPARATE ffmpeg command that reads the source directly via a plain
        // -vf (no filter_complex, so no conflict with the main command's
        // stream copy). A plan is therefore built whenever the profile targets
        // a video output and the source actually has frames to sample —
        // transcode and copy both qualify now. Audio-only (no profile.Video,
        // or no source video stream) has no frames to sprite from.
        bool hasVideoOutput = profile.Video is not null;

        if (media.VideoStreams.Count > 0 && hasVideoOutput)
        {
            // Explicit profile.Thumbnails wins. Otherwise fall back to
            // HlsDerivatives: when GenerateSpriteVtt is on (the default for HLS),
            // build a ThumbnailOutput from SpriteVttThumbnailWidth +
            // SpriteVttIntervalSeconds so sprites land for every HLS preset
            // without each author having to also set the legacy Thumbnails field.
            // Null HlsDerivatives (legacy presets) gets treated as a
            // fresh HlsDerivatives() so the sprite-on defaults still apply.
            ThumbnailOutput? thumbConfig = profile.Thumbnails;
            if (thumbConfig is null)
            {
                HlsDerivatives derivatives = profile.HlsDerivatives ?? new HlsDerivatives();
                if (derivatives.GenerateSpriteVtt)
                {
                    thumbConfig = new(
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
