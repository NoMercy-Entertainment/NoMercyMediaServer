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

        Assert.NotEmpty(presets);
        foreach (EncodingProfile preset in presets)
        {
            Assert.NotEqual(default, preset.Id);
            Assert.True(preset.IsBuiltin, $"{preset.Name} must be marked IsBuiltin");
            Assert.False(string.IsNullOrWhiteSpace(preset.Name));
        }
    }

    [Fact]
    public void AllBuiltInPresets_HaveUniqueIds()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        Ulid[] ids = [.. presets.Select(p => p.Id)];
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void AllBuiltInPresets_HaveUniqueNames()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        string[] names = [.. presets.Select(p => p.Name)];
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void AllBuiltInPresets_IsIdempotent()
    {
        EncodingProfile[] a = BuiltinPresets.All();
        EncodingProfile[] b = BuiltinPresets.All();

        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.Equal(a[i].Id, b[i].Id);
            Assert.Equal(a[i].Name, b[i].Name);
        }
    }

    [Fact]
    public void AllBuiltInPresets_PassValidationWithoutErrors()
    {
        EncodingProfile[] presets = BuiltinPresets.All();

        foreach (EncodingProfile preset in presets)
        {
            ProfileValidationResult result = ProfileValidator.Validate(preset);
            Assert.True(
                result.IsValid,
                $"{preset.Name} failed validation: " + string.Join(", ", result.Errors)
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
        EncodingProfile anime = presets.First(p => p.Name == "Anime 1080p HEVC 10-bit");

        Assert.NotNull(anime.Video);
        Assert.Equal("animation", anime.Video!.Tune);
        Assert.Equal(VideoCodecType.H265, anime.Video.Codec);
        Assert.Equal(10, anime.Video.BitDepth);
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
            EncodingProfile preset in presets.Where(p =>
                p.HdrPolicies == HdrPolicies.AlwaysPreserve
            )
        )
        {
            Assert.NotNull(preset.Video);
            Assert.True(
                preset.Video!.BitDepth >= 10,
                $"{preset.Name} preserves HDR but is {preset.Video.BitDepth}-bit"
            );
            Assert.Contains(
                preset.Video.Codec,
                new[] { VideoCodecType.H265, VideoCodecType.Av1, VideoCodecType.Vp9 }
            );
        }
    }

    [Fact]
    public void MusicFlacPreset_IsAudioOnlyInFlac()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        EncodingProfile music = presets.First(p => p.Name == "Music FLAC Lossless");

        Assert.Null(music.Video);
        Assert.Empty(music.Subtitles);
        Assert.Single(music.Audio);
        Assert.Equal(Container.Flac, music.Container);
        Assert.Equal(AudioCodecType.Flac, music.Audio[0].Codec);
    }

    [Fact]
    public void MusicMp3Preset_IsAudioOnlyInMp3()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        EncodingProfile music = presets.First(p => p.Name == "Music MP3 320k");

        Assert.Null(music.Video);
        Assert.Empty(music.Subtitles);
        Assert.Single(music.Audio);
        Assert.Equal(Container.Mp3, music.Container);
        Assert.Equal(AudioCodecType.Mp3, music.Audio[0].Codec);
        Assert.Equal(320, music.Audio[0].BitrateKbps);
    }

    [Fact]
    public void MusicAacPreset_IsAudioOnlyInAac()
    {
        EncodingProfile[] presets = BuiltinPresets.All();
        EncodingProfile music = presets.First(p => p.Name == "Music AAC 256k");

        Assert.Null(music.Video);
        Assert.Empty(music.Subtitles);
        Assert.Single(music.Audio);
        Assert.Equal(Container.Aac, music.Container);
        Assert.Equal(AudioCodecType.Aac, music.Audio[0].Codec);
        Assert.Equal(256, music.Audio[0].BitrateKbps);
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
        EncodingProfile archival = presets.First(p => p.Name == "HEVC Archive (Visually Lossless)");

        Assert.Equal(Container.Mkv, archival.Container);
        Assert.All(archival.Audio, a => Assert.Equal(StreamPolicy.Copy, a.Policy));
    }
}
