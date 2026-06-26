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

using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles.V2;

/// <summary>
/// Asserts the per-preset HlsDerivatives overrides specified in Plan 4 / spec table.
/// Presets without an explicit override carry HlsDerivatives = null (use defaults).
/// Presets that have new() {} (empty override) carry an explicit record equal to defaults.
/// </summary>
public class BuiltinPresetsHlsDerivativesTests
{
    private static EncodingProfile Find(string name) =>
        BuiltinPresets.All().Single(p => p.Name == name);

    // ── I-frame playlists stay disabled across every preset ──────────────────
    // GenerateIFramePlaylists=true was dropped from all built-in presets in
    // e4836ec4 because no IFramePlaylistGenerator is wired yet — FinalizeStage
    // throws NotSupportedException for any preset that opted in. The presets
    // below are the ones that previously carried the override.

    [Theory]
    [InlineData("ABR Premium HEVC 720/1080/2160")]
    [InlineData("Chromecast 1080p")]
    [InlineData("Chromecast 4K HEVC")]
    public void Hls_Presets_That_Previously_Enabled_IFramePlaylists_Now_Default_False(string name)
    {
        EncodingProfile preset = Find(name);

        HlsDerivatives effective = preset.HlsDerivatives ?? new HlsDerivatives();
        effective.GenerateIFramePlaylists.Should().BeFalse();
    }

    // ── Full Remux HLS — GenerateFontsJson = false ───────────────────────────

    [Fact]
    public void FullRemuxHls_HasFontsJson_False()
    {
        EncodingProfile preset = Find("Full Remux HLS");

        preset.HlsDerivatives.Should().NotBeNull();
        preset.HlsDerivatives!.GenerateFontsJson.Should().BeFalse();
    }

    // ── Direct Stream Audio HLS — no sprites, no thumbnail track, no fonts ───

    [Fact]
    public void DirectStreamAudioHls_SpriteVtt_False()
    {
        EncodingProfile preset = Find("Direct Stream Audio HLS");

        preset.HlsDerivatives.Should().NotBeNull();
        preset.HlsDerivatives!.GenerateSpriteVtt.Should().BeFalse();
    }

    [Fact]
    public void DirectStreamAudioHls_ThumbnailTrack_False()
    {
        EncodingProfile preset = Find("Direct Stream Audio HLS");

        preset.HlsDerivatives!.GenerateThumbnailTrack.Should().BeFalse();
    }

    [Fact]
    public void DirectStreamAudioHls_FontsJson_False()
    {
        EncodingProfile preset = Find("Direct Stream Audio HLS");

        preset.HlsDerivatives!.GenerateFontsJson.Should().BeFalse();
    }

    // ── Mobile 480p Low Bandwidth — smaller sprites ───────────────────────────

    [Fact]
    public void Mobile480p_HasSpriteVttIntervalSeconds_20()
    {
        EncodingProfile preset = Find("Mobile 480p Low Bandwidth");

        preset.HlsDerivatives.Should().NotBeNull();
        preset.HlsDerivatives!.SpriteVttIntervalSeconds.Should().Be(20);
    }

    [Fact]
    public void Mobile480p_HasSpriteVttThumbnailWidth_80()
    {
        EncodingProfile preset = Find("Mobile 480p Low Bandwidth");

        preset.HlsDerivatives!.SpriteVttThumbnailWidth.Should().Be(80);
    }

    // ── Presets that keep defaults — HlsDerivatives not null, IFramePlaylists = false ──

    [Theory]
    [InlineData("Web 1080p Balanced")]
    [InlineData("Anime HEVC 1080p 10-bit")]
    [InlineData("Smart Remux HLS")]
    public void HlsPresets_WithoutOverride_HaveIFramePlaylists_AtDefaultFalse(string name)
    {
        EncodingProfile preset = Find(name);

        // These presets set HlsDerivatives = new() which is explicitly non-null
        // but defaults GenerateIFramePlaylists = false.
        HlsDerivatives effective = preset.HlsDerivatives ?? new HlsDerivatives();
        effective.GenerateIFramePlaylists.Should().BeFalse();
    }
}
