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
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

public class SpeedIndexTests
{
    [Fact]
    public void GetSpeed_ExistingKey_ReturnsMeasurement()
    {
        SpeedKey key = new(Codec: VideoCodecType.H264, Encoder: "h264_nvenc", Width: 1920, DeviceName: "RTX 4090");
        SpeedMeasurement measurement = new(Fps: 120.0, SpeedMultiplier: 5.0, MeasuredAt: DateTime.UtcNow);
        SpeedIndex index = new(Measurements: new() { [key: key] = measurement });

        SpeedMeasurement? result = index.GetSpeed(
            codec: VideoCodecType.H264,
            encoder: "h264_nvenc",
            width: 1920,
            deviceName: "RTX 4090"
        );

        result.Should().NotBeNull();
        result!.Fps.Should().Be(expected: 120.0);
        result.SpeedMultiplier.Should().Be(expected: 5.0);
    }

    [Fact]
    public void GetSpeed_NonExistentKey_ReturnsNull()
    {
        SpeedIndex index = new(Measurements: new());
        SpeedMeasurement? result = index.GetSpeed(
            codec: VideoCodecType.H264,
            encoder: "h264_nvenc",
            width: 1920,
            deviceName: "RTX 4090"
        );
        result.Should().BeNull();
    }

    [Fact]
    public void GetSpeedMultiplier_ExistingKey_ReturnsValue()
    {
        SpeedKey key = new(Codec: VideoCodecType.H265, Encoder: "hevc_nvenc", Width: 1080, DeviceName: "RTX 4090");
        SpeedMeasurement measurement = new(Fps: 60.0, SpeedMultiplier: 2.5, MeasuredAt: DateTime.UtcNow);
        SpeedIndex index = new(Measurements: new() { [key: key] = measurement });

        double mult = index.GetSpeedMultiplier(codec: VideoCodecType.H265, encoder: "hevc_nvenc", width: 1080, deviceName: "RTX 4090");
        mult.Should().Be(expected: 2.5);
    }

    [Fact]
    public void GetSpeedMultiplier_NonExistentKey_ReturnsZero()
    {
        SpeedIndex index = new(Measurements: new());
        double mult = index.GetSpeedMultiplier(codec: VideoCodecType.H265, encoder: "hevc_nvenc", width: 1080, deviceName: null);
        mult.Should().Be(expected: 0);
    }

    [Fact]
    public void SpeedKey_DifferentDevices_AreDifferentKeys()
    {
        SpeedKey key1 = new(Codec: VideoCodecType.H264, Encoder: "h264_nvenc", Width: 1920, DeviceName: "RTX 4090");
        SpeedKey key2 = new(Codec: VideoCodecType.H264, Encoder: "h264_nvenc", Width: 1920, DeviceName: "RTX 3080");
        key1.Should().NotBe(unexpected: key2);
    }

    [Fact]
    public void SpeedKey_SameValues_AreEqual()
    {
        SpeedKey key1 = new(Codec: VideoCodecType.H264, Encoder: "h264_nvenc", Width: 1920, DeviceName: "RTX 4090");
        SpeedKey key2 = new(Codec: VideoCodecType.H264, Encoder: "h264_nvenc", Width: 1920, DeviceName: "RTX 4090");
        key1.Should().Be(expected: key2);
    }

    [Fact]
    public void SpeedKey_NullDevice_MatchesNullDevice()
    {
        SpeedKey key1 = new(Codec: VideoCodecType.Av1, Encoder: "libsvtav1", Width: 1920, DeviceName: null);
        SpeedKey key2 = new(Codec: VideoCodecType.Av1, Encoder: "libsvtav1", Width: 1920, DeviceName: null);
        key1.Should().Be(expected: key2);
    }
}
