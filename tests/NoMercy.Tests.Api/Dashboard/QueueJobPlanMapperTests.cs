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

using Newtonsoft.Json;
using NoMercy.Api.DTOs.Dashboard;
using NoMercy.Api.Services;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// A queued card has no progress to show, so what it says about the work ahead
/// is the whole of its content. Every profile here arrives the way a real one
/// does — serialized to <c>ProfileJson</c> and resolved back through
/// <see cref="PresetResolver"/> — because that round trip is where a field
/// silently reverts to its default.
/// </summary>
[Trait("Category", "Unit")]
public class QueueJobPlanMapperTests
{
    private sealed class SinglePresetLookup(EncodingProfile profile) : IPresetLookup
    {
        public (string ProfileJson, Ulid? ParentPresetId)? Get(Ulid presetId)
        {
            return presetId == profile.Id ? (JsonConvert.SerializeObject(profile), null) : null;
        }
    }

    private static QueueJobPlanDto PlanFor(EncodingProfile profile)
    {
        return QueueJobPlanMapper.FromProfile(
            PresetResolver.Resolve(profile.Id, new SinglePresetLookup(profile))
        );
    }

    private static EncodingProfile Builtin(string name)
    {
        return BuiltinPresets.All().Single(profile => profile.Name == name);
    }

    [Fact]
    public void EveryShippedPreset_DescribesSomethingItWillProduce()
    {
        foreach (EncodingProfile profile in BuiltinPresets.All())
        {
            QueueJobPlanDto plan = PlanFor(profile);

            plan.Container.Should().Be(profile.Container.ToString());
            (plan.Video.Length + plan.Audio.Length + plan.Subtitles.Length)
                .Should()
                .BeGreaterThan(0, $"{profile.Name} has to say what it produces");
        }
    }

    [Fact]
    public void AnAutoLadder_ReportsItsTiersAsACeiling()
    {
        EncodingProfile profile = Builtin(BuiltinPresets.DefaultStreamingPresetName);
        QueueJobPlanDto plan = PlanFor(profile);

        plan.VideoMode.Should().Be(QueueJobPlanDto.CappedVideo);
        plan.Video.Should().NotBeEmpty();
        plan.Video.Select(rendition => rendition.Height)
            .Should()
            .BeInDescendingOrder("the tallest rendition is the one worth reading first");
    }

    [Fact]
    public void AManualLadder_ReportsExactlyTheRungsItWillEncode()
    {
        EncodingProfile profile = Builtin(BuiltinPresets.DefaultStreamingPresetName) with
        {
            Ladder = new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(1920, 1080, VideoCodecType.H264, 6000, 9000, 12000, 24),
                    new(1280, 720, VideoCodecType.H264, 3000, 4500, 6000, 24),
                ],
            },
        };

        QueueJobPlanDto plan = PlanFor(profile);

        plan.VideoMode.Should().Be(QueueJobPlanDto.FixedVideo);
        plan.Video.Select(rendition => rendition.Height).Should().Equal(1080, 720);
    }

    [Fact]
    public void AMixedCodecLadder_SplitsCodecsAtTheSameHeightTheGeneratorDoes()
    {
        EncodingProfile source = Builtin(BuiltinPresets.DefaultStreamingPresetName);
        EncodingProfile profile = source with
        {
            Ladder = new()
            {
                Mode = LadderMode.Auto,
                AutoConfig = new()
                {
                    Tiers = LadderTiers.AppleHlsRecommended,
                    CodecPolicy = LadderCodecPolicy.Mixed,
                    LowTierCodec = VideoCodecType.H264,
                    HighTierCodec = VideoCodecType.H265,
                    MixedPolicySplitHeight = 720,
                },
            },
        };

        QueueJobPlanDto plan = PlanFor(profile);

        plan.Video.Where(rendition => rendition.Height <= 720)
            .Should()
            .OnlyContain(rendition => rendition.Codec == nameof(VideoCodecType.H264));
        plan.Video.Where(rendition => rendition.Height > 720)
            .Should()
            .OnlyContain(rendition => rendition.Codec == nameof(VideoCodecType.H265));
    }

    [Fact]
    public void ARemuxPreset_SaysItCopiesRatherThanNamingAResolution()
    {
        EncodingProfile? remux = BuiltinPresets
            .All()
            .FirstOrDefault(profile => profile.Video?.Codec == VideoCodecType.Copy);

        remux.Should().NotBeNull("a copy-video preset is what this describes");

        QueueJobPlanDto plan = PlanFor(remux!);

        plan.VideoMode.Should().Be(QueueJobPlanDto.FixedVideo);
        plan.Video.Should().ContainSingle();
        plan.Video[0].Codec.Should().Be(nameof(VideoCodecType.Copy));
        plan.Video[0].Height.Should().BeNull();
    }

    [Fact]
    public void OmittedOutputs_AreNotListedAsWorkTheJobWillDo()
    {
        EncodingProfile source = Builtin(BuiltinPresets.DefaultStreamingPresetName);
        EncodingProfile profile = source with
        {
            Subtitles = source
                .Subtitles.Select(subtitle => subtitle with { Policy = SubtitlePolicy.Omit })
                .ToArray(),
        };

        PlanFor(profile).Subtitles.Should().BeEmpty();
    }

    [Fact]
    public void AudioTracks_CarryTheirChannelsBitrateAndLanguages()
    {
        EncodingProfile profile = Builtin(BuiltinPresets.DefaultStreamingPresetName);
        QueueJobPlanDto plan = PlanFor(profile);

        plan.Audio.Should().NotBeEmpty();

        foreach (PlannedAudioDto track in plan.Audio)
        {
            track.Codec.Should().NotBeNullOrWhiteSpace();
            track.Channels.Should().BeGreaterThan(0);
            track.Languages.Should().NotBeNull();
        }
    }
}
