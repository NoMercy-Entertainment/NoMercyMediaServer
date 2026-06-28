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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.LiveTranscode;

public class LiveQualitySelector(ICodecResolver codecResolver, IHardwareCapabilities hardware)
    : ILiveQualitySelector
{
    // Standard resolution tiers, from highest to lowest
    private static readonly (int Width, int Height)[] ResolutionTiers =
    [
        (3840, 2160),
        (1920, 1080),
        (1280, 720),
        (854, 480),
    ];

    public LiveQuality[] GetAvailableQualities(
        MediaInfo input,
        ClientCapabilities client,
        SpeedIndex speeds,
        IResourceBudget budget
    )
    {
        VideoStreamInfo? primaryVideo = input.VideoStreams.Count > 0 ? input.VideoStreams[0] : null;
        int sourceWidth = primaryVideo?.Width ?? 1920;
        int sourceHeight = primaryVideo?.Height ?? 1080;

        VideoCodecType targetCodec = ResolveTargetCodec(client);
        ResolvedCodec resolved = codecResolver.Resolve(targetCodec, hardware);

        string? deviceName = resolved.Device?.Name;
        bool isHardwareAccelerated = resolved.Device is not null;

        List<LiveQuality> qualities = [];

        foreach ((int tierWidth, int tierHeight) in ResolutionTiers)
        {
            // Skip resolutions larger than the source
            if (tierWidth > sourceWidth || tierHeight > sourceHeight)
                continue;

            int bitrateKbps = EstimateBitrateKbps(tierWidth, tierHeight);

            double speedMultiplier = speeds.GetSpeedMultiplier(
                targetCodec,
                resolved.FfmpegEncoderName,
                tierWidth,
                deviceName
            );

            bool canRealtime = speedMultiplier >= 1.2;

            string qualityId = $"{tierHeight}p";
            string label = $"{tierHeight}p";

            LiveQuality quality = new(
                Id: qualityId,
                Label: label,
                Width: tierWidth,
                Height: tierHeight,
                Codec: targetCodec,
                BitrateKbps: bitrateKbps,
                Encoder: resolved.FfmpegEncoderName,
                IsHardwareAccelerated: isHardwareAccelerated,
                ExpectedSpeed: speedMultiplier,
                CanRealtime: canRealtime
            );

            qualities.Add(quality);
        }

        return [.. qualities];
    }

    public LiveQuality SelectOptimal(
        MediaInfo input,
        ClientCapabilities client,
        SpeedIndex speeds,
        IResourceBudget budget
    )
    {
        LiveQuality[] candidates = GetAvailableQualities(input, client, speeds, budget);

        // Filter by client capabilities
        IEnumerable<LiveQuality> allowed = candidates.Where(q =>
            q.Width <= client.MaxWidth
            && q.Height <= client.MaxHeight
            && (
                client.SupportedVideoCodecs.Length == 0
                || client.SupportedVideoCodecs.Contains(q.Codec)
            )
        );

        // Pick highest CanRealtime quality
        LiveQuality? optimal = allowed
            .Where(q => q.CanRealtime)
            .OrderByDescending(q => q.Width)
            .ThenByDescending(q => q.Height)
            .FirstOrDefault();

        // No CanRealtime candidates → fall back to lowest resolution
        if (optimal is null)
        {
            optimal = allowed.OrderBy(q => q.Width).ThenBy(q => q.Height).FirstOrDefault();
        }

        // Absolute fallback: use the single lowest tier from all candidates
        optimal ??= candidates.OrderBy(q => q.Width).ThenBy(q => q.Height).First();

        return optimal;
    }

    // Choose the preferred video codec: pick from the client's supported codecs in priority order
    private static VideoCodecType ResolveTargetCodec(ClientCapabilities client)
    {
        VideoCodecType[] preferred =
        [
            VideoCodecType.H265,
            VideoCodecType.H264,
            VideoCodecType.Vp9,
            VideoCodecType.Av1,
        ];

        // Only consider codecs the client explicitly listed as supported.
        if (client.SupportedVideoCodecs.Length > 0)
        {
            foreach (VideoCodecType codec in preferred)
            {
                if (client.SupportedVideoCodecs.Contains(codec))
                    return codec;
            }
        }

        // Explicit fallback — empty list or no intersection. H264 is almost
        // universally supported.
        return VideoCodecType.H264;
    }

    private static int EstimateBitrateKbps(int width, int height) =>
        (width, height) switch
        {
            (3840, 2160) => 20000,
            (1920, 1080) => 8000,
            (1280, 720) => 4000,
            _ => 2000,
        };
}
