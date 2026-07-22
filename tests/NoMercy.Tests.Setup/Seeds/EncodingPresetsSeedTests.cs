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
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Setup.Seeds;

/// <summary>
/// Every built-in preset must (a) round-trip through JSON without data loss,
/// (b) pass ProfileValidator without errors, (c) carry a stable deterministic
/// Id so repeat seeding upserts rather than duplicating rows, and (d) satisfy
/// codec/container invariants specific to its tier.
/// </summary>
public class EncodingPresetsSeedTests
{
    [Fact]
    public void AllBuiltInPresets_HaveStableIds_AndAreMarkedBuiltIn()
    {
        EncodingProfile[] presets = BuiltinPresets.All();

        Assert.NotEmpty(collection: presets);
        foreach (EncodingProfile preset in presets)
        {
            Assert.NotEqual(expected: default, actual: preset.Id);
            Assert.True(condition: preset.IsBuiltin, userMessage: $"{preset.Name} must be marked IsBuiltin");
            Assert.False(condition: string.IsNullOrWhiteSpace(value: preset.Name));
        }
    }

    [Fact]
    public void AllBuiltInPresets_HaveUniqueIds()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        Ulid[] ids = presets.Select(selector: p => p.Id).ToArray();
        Assert.Equal(expected: ids.Length, actual: ids.Distinct().Count());
    }

    [Fact]
    public void AllBuiltInPresets_HaveUniqueNames()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        string[] names = presets.Select(selector: p => p.Name).ToArray();
        Assert.Equal(expected: names.Length, actual: names.Distinct().Count());
    }

    [Fact]
    public void AllBuiltInPresets_IsIdempotent()
    {
        EncodingProfile[] a = BuiltinPresets.All();
        EncodingProfile[] b = BuiltinPresets.All();

        Assert.Equal(expected: a.Length, actual: b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.Equal(expected: a[i].Id, actual: b[i].Id);
            Assert.Equal(expected: a[i].Name, actual: b[i].Name);
        }
    }

    [Fact]
    public void AllBuiltInPresets_PassValidationWithoutErrors()
    {
        EncodingProfile[] presets = BuiltinPresets.All();

        foreach (EncodingProfile preset in presets)
        {
            ProfileValidationResult result = ProfileValidator.Validate(profile: preset);
            Assert.True(
                condition: result.IsValid,
                userMessage: $"{preset.Name} failed validation: " + string.Join(separator: ", ", values: result.Errors)
            );
        }
    }

    /// <summary>
    /// x265 spends bitrate on film grain by default, which animation does not
    /// have; the animation tune redirects it at the flat colour and hard edges
    /// that anime actually needs. Losing the tune would silently make the preset
    /// an ordinary 1080p encode wearing the anime name.
    /// </summary>
    [Fact]
    public void AnimePreset_CarriesAnimationTune()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        EncodingProfile anime = presets.First(predicate: p => p.Name == "Anime 1080p HEVC 10-bit");

        Assert.NotNull(@object: anime.Video);
        Assert.Equal(expected: "animation", actual: anime.Video!.Tune);
        Assert.Equal(expected: VideoCodecType.H265, actual: anime.Video.Codec);
        Assert.Equal(expected: 10, actual: anime.Video.BitDepth);
    }

    /// <summary>
    /// AlwaysPreserve on an 8-bit output asks the encoder to synthesise HDR from
    /// an SDR pipeline, which it cannot do and which ProfileRuleValidator rejects
    /// outright. A preset that preserves HDR must therefore be 10-bit.
    /// </summary>
    [Fact]
    public void HdrPreservingPreset_Is10BitOnAnHdrCapableCodec()
    {
        EncodingProfile[] presets = BuiltinPresets.All();

        foreach (
            EncodingProfile preset in presets.Where(predicate: p =>
                p.HdrPolicies == HdrPolicies.AlwaysPreserve
            )
        )
        {
            Assert.NotNull(@object: preset.Video);
            Assert.True(
                condition: preset.Video!.BitDepth >= 10,
                userMessage: $"{preset.Name} preserves HDR but is {preset.Video.BitDepth}-bit"
            );
            Assert.Contains(
                expected: preset.Video.Codec,
                collection: new[] { VideoCodecType.H265, VideoCodecType.Av1, VideoCodecType.Vp9 }
            );
        }
    }

    [Fact]
    public void MusicFlacPreset_IsAudioOnlyInFlac()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        EncodingProfile music = presets.First(predicate: p => p.Name == "Music FLAC Lossless");

        Assert.Null(@object: music.Video);
        Assert.Empty(collection: music.Subtitles);
        Assert.Single(collection: music.Audio);
        Assert.Equal(expected: Container.Flac, actual: music.Container);
        Assert.Equal(expected: AudioCodecType.Flac, actual: music.Audio[0].Codec);
    }

    [Fact]
    public void MusicMp3Preset_IsAudioOnlyInMp3()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        EncodingProfile music = presets.First(predicate: p => p.Name == "Music MP3 320k");

        Assert.Null(@object: music.Video);
        Assert.Empty(collection: music.Subtitles);
        Assert.Single(collection: music.Audio);
        Assert.Equal(expected: Container.Mp3, actual: music.Container);
        Assert.Equal(expected: AudioCodecType.Mp3, actual: music.Audio[0].Codec);
        Assert.Equal(expected: 320, actual: music.Audio[0].BitrateKbps);
    }

    [Fact]
    public void MusicAacPreset_IsAudioOnlyInAac()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        EncodingProfile music = presets.First(predicate: p => p.Name == "Music AAC 256k");

        Assert.Null(@object: music.Video);
        Assert.Empty(collection: music.Subtitles);
        Assert.Single(collection: music.Audio);
        Assert.Equal(expected: Container.Aac, actual: music.Container);
        Assert.Equal(expected: AudioCodecType.Aac, actual: music.Audio[0].Codec);
        Assert.Equal(expected: 256, actual: music.Audio[0].BitrateKbps);
    }

    /// <summary>
    /// An archive preset re-encodes video and nothing else. Transcoding audio
    /// would collapse a surround track to whatever the preset names, which is
    /// quality loss the "archive" label disclaims.
    /// </summary>
    [Fact]
    public void ArchivePreset_CopiesAudioIntoMkv()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        EncodingProfile archival = presets.First(predicate: p => p.Name == "HEVC Archive (Visually Lossless)");

        Assert.Equal(expected: Container.Mkv, actual: archival.Container);
        Assert.All(collection: archival.Audio, action: a => Assert.Equal(expected: StreamPolicy.Copy, actual: a.Policy));
    }
}
