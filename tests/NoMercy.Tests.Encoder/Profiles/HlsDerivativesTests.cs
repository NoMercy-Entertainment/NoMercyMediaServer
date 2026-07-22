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
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class HlsDerivativesTests
{
    // ── 1. Default field values ───────────────────────────────────────────────

    [Fact]
    public void Defaults_MatchSpec()
    {
        HlsDerivatives d = new();

        d.GenerateMetadataJson.Should().BeTrue();
        d.GenerateSpriteVtt.Should().BeTrue();
        d.SpriteVttIntervalSeconds.Should().Be(expected: 10);
        d.SpriteVttColumns.Should().Be(expected: 5);
        d.SpriteVttRows.Should().Be(expected: 5);
        d.SpriteVttThumbnailWidth.Should().Be(expected: 160);
        d.GenerateChapters.Should().BeTrue();
        d.GenerateFontsJson.Should().BeTrue();
        d.GenerateIFramePlaylists.Should().BeFalse();
        d.GenerateThumbnailTrack.Should().BeTrue();
        d.ExtractClosedCaptions.Should().BeFalse();
        d.GenerateMasterPlaylist.Should().BeTrue();
        d.WriteOriginalFilename.Should().BeTrue();
    }

    // ── 2. Record equality ────────────────────────────────────────────────────

    [Fact]
    public void TwoDefaults_AreEqual()
    {
        HlsDerivatives a = new();
        HlsDerivatives b = new();

        a.Should().Be(expected: b);
    }

    // ── 3. Newtonsoft.Json round-trip ─────────────────────────────────────────

    [Fact]
    public void JsonRoundTrip_PreservesAllFields()
    {
        HlsDerivatives original = new()
        {
            GenerateMetadataJson = false,
            GenerateSpriteVtt = false,
            SpriteVttIntervalSeconds = 30,
            SpriteVttColumns = 8,
            SpriteVttRows = 8,
            SpriteVttThumbnailWidth = 240,
            GenerateChapters = false,
            GenerateFontsJson = false,
            GenerateIFramePlaylists = true,
            GenerateThumbnailTrack = false,
            ExtractClosedCaptions = true,
            GenerateMasterPlaylist = false,
            WriteOriginalFilename = false,
        };

        string json = JsonConvert.SerializeObject(value: original);
        HlsDerivatives? deserialized = JsonConvert.DeserializeObject<HlsDerivatives>(value: json);

        deserialized.Should().NotBeNull();
        deserialized.Should().Be(expected: original);
    }

    // ── 4. `with` doesn't mutate other fields ─────────────────────────────────

    [Fact]
    public void With_OneField_LeavesOthersAtDefaults()
    {
        HlsDerivatives defaults = new();
        HlsDerivatives modified = defaults with { GenerateIFramePlaylists = true };

        modified.GenerateIFramePlaylists.Should().BeTrue();
        modified.GenerateMetadataJson.Should().Be(expected: defaults.GenerateMetadataJson);
        modified.GenerateSpriteVtt.Should().Be(expected: defaults.GenerateSpriteVtt);
        modified.SpriteVttIntervalSeconds.Should().Be(expected: defaults.SpriteVttIntervalSeconds);
        modified.SpriteVttColumns.Should().Be(expected: defaults.SpriteVttColumns);
        modified.SpriteVttRows.Should().Be(expected: defaults.SpriteVttRows);
        modified.SpriteVttThumbnailWidth.Should().Be(expected: defaults.SpriteVttThumbnailWidth);
        modified.GenerateChapters.Should().Be(expected: defaults.GenerateChapters);
        modified.GenerateFontsJson.Should().Be(expected: defaults.GenerateFontsJson);
        modified.GenerateThumbnailTrack.Should().Be(expected: defaults.GenerateThumbnailTrack);
        modified.ExtractClosedCaptions.Should().Be(expected: defaults.ExtractClosedCaptions);
        modified.GenerateMasterPlaylist.Should().Be(expected: defaults.GenerateMasterPlaylist);
        modified.WriteOriginalFilename.Should().Be(expected: defaults.WriteOriginalFilename);
    }
}
