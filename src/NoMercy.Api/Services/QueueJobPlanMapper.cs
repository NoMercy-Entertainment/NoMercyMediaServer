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

using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Api.Services;

/// <summary>
/// Reads an encoding profile as the outputs it is going to produce, for a queue
/// card that has nothing else to show yet.
///
/// <para>An auto ladder is reported as its configured tiers rather than the
/// rungs the generator will settle on. Those depend on the source — a 720p file
/// under a 4K ladder gets neither the 4K nor the 1440p rung — and nothing has
/// probed the file while the job is still waiting. Reporting the tiers with
/// <see cref="QueueJobPlanDto.CappedVideo"/> says that plainly; guessing the
/// rungs would put resolutions on the card that never get encoded.</para>
/// </summary>
public static class QueueJobPlanMapper
{
    public static QueueJobPlanDto FromProfile(EncodingProfile profile)
    {
        (PlannedVideoDto[] Renditions, string Mode) video = DescribeVideo(profile);

        return new()
        {
            Container = profile.Container.ToString(),
            Video = video.Renditions,
            VideoMode = video.Mode,
            Audio = DescribeAudio(profile),
            Subtitles = DescribeSubtitles(profile),
        };
    }

    /// <summary>
    /// Several presets on one folder as the single encode they actually are.
    ///
    /// <para>The runner unions them into one coordinated run — shared analysis,
    /// audio and subtitles, one master playlist listing every preset's
    /// renditions — so listing one preset's outputs would describe a fraction
    /// of the work. Duplicates across presets collapse for the same reason:
    /// two presets that both want 1080p H.264 produce it once.</para>
    ///
    /// <para>The container is the first preset's. That is the primary in the
    /// encoder's own merge, the one whose source-derived plan fields win.</para>
    /// </summary>
    public static QueueJobPlanDto Merge(IReadOnlyList<QueueJobPlanDto> plans)
    {
        if (plans.Count == 1)
            return plans[0];

        return new()
        {
            Container = plans.Count == 0 ? string.Empty : plans[0].Container,
            Video = plans
                .SelectMany(plan => plan.Video)
                .DistinctBy(rendition => (rendition.Codec, rendition.Height))
                .OrderByDescending(rendition => rendition.Height)
                .ToArray(),
            // A ceiling anywhere makes the whole list a ceiling: the source
            // decides how much of it gets encoded either way.
            VideoMode = plans.Any(plan => plan.VideoMode == QueueJobPlanDto.CappedVideo)
                ? QueueJobPlanDto.CappedVideo
                : QueueJobPlanDto.FixedVideo,
            Audio = plans
                .SelectMany(plan => plan.Audio)
                .DistinctBy(track =>
                    (
                        track.Codec,
                        track.Channels,
                        track.BitrateKbps,
                        string.Join(',', track.Languages)
                    )
                )
                .ToArray(),
            Subtitles = plans
                .SelectMany(plan => plan.Subtitles)
                .DistinctBy(subtitle =>
                    (subtitle.Codec, subtitle.Policy, string.Join(',', subtitle.Languages))
                )
                .ToArray(),
        };
    }

    private static (PlannedVideoDto[] Renditions, string Mode) DescribeVideo(
        EncodingProfile profile
    )
    {
        VideoOutput? video = profile.Video;

        if (video is null || video.Policy == StreamPolicy.Omit)
            return ([], QueueJobPlanDto.FixedVideo);

        if (video.Policy == StreamPolicy.Copy || video.Codec == VideoCodecType.Copy)
            return ([new() { Codec = nameof(VideoCodecType.Copy) }], QueueJobPlanDto.FixedVideo);

        LadderConfig? ladder = profile.Ladder;

        if (ladder is { Mode: LadderMode.Manual, Rungs.Length: > 0 })
            return (
                ladder
                    .Rungs.Select(rung => new PlannedVideoDto
                    {
                        Codec = rung.Codec.ToString(),
                        Height = rung.Height,
                    })
                    .ToArray(),
                QueueJobPlanDto.FixedVideo
            );

        if (ladder is { Mode: LadderMode.Auto })
        {
            AutoLadderConfig auto = ladder.AutoConfig ?? new();

            return (
                auto.Tiers.OrderByDescending(tier => tier.Height)
                    .Select(tier => new PlannedVideoDto
                    {
                        Codec = TierCodec(auto, video.Codec, tier.Height).ToString(),
                        Height = tier.Height,
                    })
                    .ToArray(),
                QueueJobPlanDto.CappedVideo
            );
        }

        return (
            [new() { Codec = video.Codec.ToString(), Height = video.Height }],
            QueueJobPlanDto.FixedVideo
        );
    }

    /// <summary>
    /// The split the generator applies: at or below the split height the low
    /// tier codec, above it the high tier one. A Mixed ladder missing either
    /// codec is a profile the validator rejects, so the output codec stands in
    /// rather than throwing on a read-only dashboard call.
    /// </summary>
    private static VideoCodecType TierCodec(
        AutoLadderConfig auto,
        VideoCodecType outputCodec,
        int tierHeight
    )
    {
        if (auto.CodecPolicy != LadderCodecPolicy.Mixed)
            return outputCodec;

        return tierHeight <= auto.MixedPolicySplitHeight
            ? auto.LowTierCodec ?? outputCodec
            : auto.HighTierCodec ?? outputCodec;
    }

    private static PlannedAudioDto[] DescribeAudio(EncodingProfile profile)
    {
        return profile
            .Audio.Where(audio => audio.Policy != StreamPolicy.Omit)
            .Select(audio => new PlannedAudioDto
            {
                Codec = audio.Codec.ToString(),
                Channels = audio.Channels,
                BitrateKbps = audio.BitrateKbps,
                Languages = audio.AllowedLanguages,
            })
            .ToArray();
    }

    private static PlannedSubtitleDto[] DescribeSubtitles(EncodingProfile profile)
    {
        return profile
            .Subtitles.Where(subtitle => subtitle.Policy != SubtitlePolicy.Omit)
            .Select(subtitle => new PlannedSubtitleDto
            {
                Codec = subtitle.Codec.ToString(),
                Policy = subtitle.Policy.ToString(),
                Languages = subtitle.AllowedLanguages,
            })
            .ToArray();
    }
}
