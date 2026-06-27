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

using NoMercy.Data.Logic;
using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Unit")]
public class EncoderProfileParamsDtoTests
{
    [Fact]
    public void Constructor_MapsFirstVideoAndAudioProfile()
    {
        EncoderProfile profile = new()
        {
            Name = "Test Profile",
            VideoProfiles =
            [
                new VideoProfile
                {
                    Width = 1920,
                    Crf = 21,
                    Preset = "slow",
                    Profile = "high",
                    Codec = "h264",
                },
            ],
            AudioProfiles = [new AudioProfile { Codec = "aac" }],
        };

        EncoderProfileParamsDto dto = new(profile);

        Assert.Equal(1920, dto.Width);
        Assert.Equal(21, dto.Crf);
        Assert.Equal("slow", dto.Preset);
        Assert.Equal("high", dto.Profile);
        Assert.Equal("h264", dto.Codec);
        Assert.Equal("aac", dto.Audio);
    }

    [Fact]
    public void Constructor_WithNoProfiles_LeavesDefaults()
    {
        EncoderProfileParamsDto dto = new(new EncoderProfile { Name = "Test Profile" });

        Assert.Equal(0, dto.Width);
        Assert.Equal(0, dto.Crf);
        Assert.Equal(string.Empty, dto.Preset);
        Assert.Equal(string.Empty, dto.Codec);
        Assert.Equal(string.Empty, dto.Audio);
    }
}
