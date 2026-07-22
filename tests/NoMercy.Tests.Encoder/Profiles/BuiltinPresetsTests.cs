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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Profiles;

public class BuiltinPresetsTests
{
    public static TheoryData<string> BuiltinNames()
    {
        TheoryData<string> data = [];
        foreach (EncodingProfile profile in BuiltinPresets.All())
            data.Add(p: profile.Name);

        return data;
    }

    private static EncodingProfile Preset(string name) =>
        BuiltinPresets.All().Single(predicate: profile => profile.Name == name);

    [Fact]
    public void All_presets_pass_validation()
    {
        foreach (EncodingProfile profile in BuiltinPresets.All())
        {
            ProfileValidationResult result = ProfileValidator.Validate(profile: profile);
            result
                .IsValid.Should()
                .BeTrue(
                    because: $"preset '{profile.Name}' must be valid; errors: {string.Join(separator: "; ", values: result.Errors)}"
                );
        }
    }

    /// <summary>
    /// ProfileValidator only checks container/codec pairings. The rule validator
    /// is the one that catches a preset ffmpeg would reject at runtime (an
    /// unusable width, a rate control with no target), so a built-in has to
    /// survive both or it fails on a real encode rather than in CI.
    /// </summary>
    [Theory]
    [MemberData(memberName: nameof(BuiltinNames))]
    public void Every_preset_is_free_of_rule_errors(string name)
    {
        ValidationEnvelope envelope = ProfileRuleValidator.Validate(profile: Preset(name: name));

        envelope
            .Valid.Should()
            .BeTrue(
                because: $"preset '{name}' must be encodable; errors: {string.Join(separator: "; ", values: envelope.Errors.Select(selector: rule => $"{rule.Id}: {rule.Message}"))}"
            );
    }

    /// <summary>
    /// AutoLadderExpander derives every rung from the profile's Video output. A
    /// ladder preset that leaves Video null makes the expander log and emit no
    /// video at all, which validation does not catch because a null Video is a
    /// legitimate shape for a manual-rung profile.
    /// </summary>
    [Fact]
    public void Ladder_presets_carry_a_reference_video_output()
    {
        EncodingProfile[] ladders = BuiltinPresets
            .All()
            .Where(predicate: profile => profile.Ladder is { Mode: LadderMode.Auto })
            .ToArray();

        ladders.Should().NotBeEmpty();

        foreach (EncodingProfile profile in ladders)
        {
            profile
                .Video.Should()
                .NotBeNull(
                    because: $"auto-ladder preset '{profile.Name}' has no reference Video output, so it would encode audio only"
                );
        }
    }

    /// <summary>
    /// The reference Video codec is what the ladder actually encodes with. A
    /// preset whose name promises one codec while its reference carries another
    /// silently ships the wrong encode.
    /// </summary>
    [Theory]
    [InlineData(data: ["H.264 Streaming (Universal)", VideoCodecType.H264])]
    [InlineData(data: ["HEVC Streaming (Premium)", VideoCodecType.H265])]
    [InlineData(data: ["HEVC HDR Streaming (Premium)", VideoCodecType.H265])]
    [InlineData(data: ["AV1 Streaming (Efficient)", VideoCodecType.Av1])]
    public void Ladder_preset_reference_uses_the_codec_it_advertises(
        string name,
        VideoCodecType expected
    )
    {
        EncodingProfile preset = BuiltinPresets.All().Single(predicate: profile => profile.Name == name);

        preset.Video.Should().NotBeNull();
        preset.Video!.Codec.Should().Be(expected: expected);
    }

    /// <summary>
    /// An archive preset exists to drop bitrate and nothing else. Re-encoding
    /// audio would collapse surround to stereo and rasterising subtitles would
    /// lose PGS, both of which are quality loss the name disclaims.
    /// </summary>
    [Theory]
    [InlineData(data: "Remux (Lossless MKV)")]
    [InlineData(data: "HEVC Archive (Visually Lossless)")]
    [InlineData(data: "HEVC Space Saver")]
    [InlineData(data: "AV1 Archive (Maximum Compression)")]
    public void Archive_presets_copy_audio_and_subtitles(string name)
    {
        EncodingProfile preset = BuiltinPresets.All().Single(predicate: profile => profile.Name == name);

        preset
            .Audio.Should()
            .AllSatisfy(expected: audio => audio.Policy.Should().Be(expected: StreamPolicy.Copy))
            .And.NotBeEmpty();

        preset.Subtitles.Should().AllSatisfy(expected: sub => sub.Policy.Should().Be(expected: SubtitlePolicy.Copy));
    }

    /// <summary>
    /// Hardware encoders trade quality for speed at a given bitrate. An archive
    /// encode is written once and kept, so it must not silently land on NVENC.
    /// </summary>
    [Theory]
    [InlineData(data: "HEVC Archive (Visually Lossless)")]
    [InlineData(data: "HEVC Space Saver")]
    [InlineData(data: "AV1 Archive (Maximum Compression)")]
    public void Archive_presets_prefer_quality_over_hardware(string name)
    {
        EncodingProfile preset = BuiltinPresets.All().Single(predicate: profile => profile.Name == name);

        preset.HardwarePreference.Should().Be(expected: HardwarePreference.PreferQuality);
    }

    /// <summary>
    /// CodecTunings carries encoder flags (x265 rdoq-level, SVT-AV1 enable-qm)
    /// that only reach ffmpeg through the reference output's CustomArguments.
    /// </summary>
    [Fact]
    public void Archive_preset_carries_its_codec_tuning_arguments()
    {
        EncodingProfile preset = BuiltinPresets
            .All()
            .Single(predicate: profile => profile.Name == "AV1 Archive (Maximum Compression)");

        Dictionary<string, string>? expected = CodecTunings
            .For(codec: VideoCodecType.Av1, quality: EncodingQuality.VeryHigh)
            .ExtraArgs;

        preset.Video.Should().NotBeNull();
        preset.Video!.CustomArguments.Should().BeEquivalentTo(expectation: expected);
    }

    /// <summary>
    /// An archive or export preset must not rescale: a null width means "keep
    /// the source". Width 0 is not the same thing — it resolves to the smallest
    /// legal even frame (2px) and ffmpeg rejects the encode outright.
    /// </summary>
    [Theory]
    [InlineData(data: "HEVC Archive (Visually Lossless)")]
    [InlineData(data: "HEVC Space Saver")]
    [InlineData(data: "AV1 Archive (Maximum Compression)")]
    [InlineData(data: "H.264 MP4 (Universal)")]
    [InlineData(data: "HEVC MP4 (High Quality)")]
    public void Source_sized_presets_leave_width_unset(string name)
    {
        EncodingProfile preset = Preset(name: name);

        preset.Video.Should().NotBeNull();
        preset.Video!.Width.Should().BeNull();
        preset.Video.Height.Should().BeNull();
    }

    /// <summary>
    /// EncodingOrchestrator resolves a strategy by (format, EncodeMode) and
    /// throws when none is registered, so a two-pass preset in a container with
    /// no TwoPass strategy fails every encode. Only HLS, MP4 and DASH have one.
    /// </summary>
    [Fact]
    public void Two_pass_preset_targets_a_bitrate_in_a_two_pass_capable_container()
    {
        EncodingProfile preset = Preset(name: "H.264 MP4 1080p (2-Pass 5 Mbps)");

        preset.EncodeMode.Should().Be(expected: EncodeMode.TwoPass);
        preset
            .Container.Should()
            .BeOneOf(validValues: [Container.Mp4, Container.HlsTs, Container.HlsFmp4, Container.Dash]);

        preset.Video.Should().NotBeNull();
        preset.Video!.RateControl.Should().Be(expected: RateControlMode.Vbr);
        preset.Video.BitrateKbps.Should().BePositive(because: "two passes exist to hit a bitrate target");
        preset.Video.Crf.Should().Be(expected: 0, because: "a bitrate target and a CRF target would compete");
        preset
            .Video.Width.Should()
            .NotBeNull(because: "a bitrate target is meaningless without a frame size");
    }

    /// <summary>
    /// PlanStage only derives ConvertHdrToSdr for EmitHdrAndSdr. Under
    /// AlwaysTonemap the preset has to request the conversion itself or the flag
    /// never reaches the filter chain and the "to SDR" promise silently fails.
    /// </summary>
    [Fact]
    public void Tonemap_preset_asks_for_the_conversion_itself()
    {
        EncodingProfile preset = Preset(name: "H.264 Streaming (HDR to SDR)");

        preset.HdrPolicies.Should().Be(expected: HdrPolicies.AlwaysTonemap);
        preset.Video.Should().NotBeNull();
        preset.Video!.ConvertHdrToSdr.Should().BeTrue();
        preset.Video.BitDepth.Should().Be(expected: 8, because: "the point is an SDR output");
    }

    [Fact]
    public void Hdr_preset_emits_both_hdr_and_sdr_rungs()
    {
        EncodingProfile preset = Preset(name: "HEVC HDR Streaming (Premium)");

        preset.HdrPolicies.Should().Be(expected: HdrPolicies.EmitHdrAndSdr);
        preset.HdrOptions.Should().NotBeNull();
        preset.HdrOptions!.Force10Bit.Should().BeTrue();
    }

    /// <summary>
    /// A library preset repackages the source for streaming — it should never
    /// re-encode audio into a fixed AAC+E-AC-3 pair, since that inflates a
    /// source already carrying compact lossy tracks (e.g. two Opus stereo
    /// tracks) into several times its size for no quality gain.
    /// </summary>
    [Theory]
    [InlineData(data: "Anime 1080p HEVC 10-bit")]
    [InlineData(data: "1080p HEVC 10-bit")]
    [InlineData(data: "4K HDR HEVC 10-bit")]
    [InlineData(data: "1080p SDR HEVC 10-bit")]
    public void Library_presets_copy_audio_rather_than_transcode(string name)
    {
        EncodingProfile preset = Preset(name: name);

        preset
            .Audio.Should()
            .AllSatisfy(expected: audio => audio.Policy.Should().Be(expected: StreamPolicy.Copy))
            .And.NotBeEmpty();
    }

    /// <summary>
    /// Direct-stream audio exists to avoid re-encoding, so a transcoding audio
    /// output would defeat the whole preset.
    /// </summary>
    [Fact]
    public void Direct_stream_audio_copies_rather_than_transcodes()
    {
        EncodingProfile preset = Preset(name: "Direct Stream Audio HLS");

        preset.Container.Should().Be(expected: Container.AudioHlsFmp4);
        preset.Video.Should().BeNull();
        preset.Audio.Should().AllSatisfy(expected: audio => audio.Policy.Should().Be(expected: StreamPolicy.Copy));
    }

    [Fact]
    public void Legacy_preset_targets_legacy_devices_with_baseline()
    {
        EncodingProfile preset = Preset(name: "Legacy Device H.264 Baseline");

        preset.ClientCompatibility.Should().HaveFlag(expectedFlag: ClientCompatibility.LegacyDevices);
        preset.Video.Should().NotBeNull();
        preset.Video!.Codec.Should().Be(expected: VideoCodecType.H264);
        preset.Video.CodecProfile.Should().Be(expected: CodecProfile.Baseline);
        preset.Video.BitDepth.Should().Be(expected: 8);
    }

    [Fact]
    public void Dash_preset_is_shipped()
    {
        EncodingProfile preset = Preset(name: "DASH Streaming (Universal)");

        preset.Container.Should().Be(expected: Container.Dash);
        preset.Ladder.Should().NotBeNull();
        preset.Video.Should().NotBeNull();
    }

    [Fact]
    public void Default_streaming_preset_name_resolves_to_a_shipped_preset()
    {
        BuiltinPresets
            .All()
            .Should()
            .ContainSingle(predicate: profile => profile.Name == BuiltinPresets.DefaultStreamingPresetName);
    }

    [Fact]
    public void All_preset_ids_are_deterministic_and_unique()
    {
        EncodingProfile[] all = BuiltinPresets.All();

        all.Select(selector: profile => profile.Id).Should().OnlyHaveUniqueItems();
        BuiltinPresets
            .All()
            .Select(selector: profile => profile.Id)
            .Should()
            .BeEquivalentTo(expectation: all.Select(selector: profile => profile.Id));
    }

    [Fact]
    public void All_presets_are_marked_builtin_and_described()
    {
        BuiltinPresets
            .All()
            .Should()
            .AllSatisfy(expected: profile =>
            {
                profile.IsBuiltin.Should().BeTrue();
                profile.Description.Should().NotBeNullOrWhiteSpace();
            });
    }
}
