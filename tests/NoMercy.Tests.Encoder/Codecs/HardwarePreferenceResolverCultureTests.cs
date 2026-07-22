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

using System.Globalization;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using HardwarePreference = NoMercy.Encoder.Profiles.HardwarePreference;

namespace NoMercy.Tests.Encoder.Codecs;

/// <summary>
/// The hardware-vs-software speed ratio rides the <see cref="DecisionLog"/>
/// Message/Data the dashboard reads over the API. On a comma-decimal server
/// locale a bare ":F1" turns "2.5x" into "2,5x" in that payload — this pins
/// InvariantCulture on the formatter for both PreferHardware and
/// ForceHardware.
/// </summary>
public class HardwarePreferenceResolverCultureTests
{
    private readonly HardwarePreferenceResolver _resolver = new();

    private static SpeedIndex MakeSpeedIndex(
        params (VideoCodecType Codec, string Encoder, double Fps)[] entries
    )
    {
        Dictionary<SpeedKey, SpeedMeasurement> dict = new();

        foreach ((VideoCodecType codec, string encoder, double fps) in entries)
        {
            SpeedKey key = new(Codec: codec, Encoder: encoder, Width: 1920, DeviceName: null);
            dict[key: key] = new(Fps: fps, SpeedMultiplier: 1.0, MeasuredAt: DateTime.UtcNow);
        }

        return new(Measurements: dict);
    }

    private static List<string> WithNvenc() =>
        ["libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc"];

    [Theory]
    [InlineData(data: "de-DE")]
    [InlineData(data: "nl-NL")]
    [InlineData(data: "fr-FR")]
    public void PreferHardware_Ratio_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(name: culture);

            // 250 / 100 = 2.5x exactly — clean single-decimal ratio.
            SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 250)]
            );
            ScopedDecisionLog log = new();

            HardwareResolutionResult result = _resolver.Resolve(
                codec: VideoCodecType.H264,
                preference: HardwarePreference.PreferHardware,
                availableEncoders: WithNvenc(),
                speedIndex: index,
                decisions: log
            );

            Assert.Equal(expected: "h264_nvenc", actual: result.EncoderHandle);
            string message = log.Snapshot()[index: 0].Message;
            Assert.Contains(expectedSubstring: "2.5", actualString: message);
            Assert.DoesNotContain(expectedSubstring: "2,5", actualString: message);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(data: "de-DE")]
    [InlineData(data: "nl-NL")]
    [InlineData(data: "fr-FR")]
    public void ForceHardware_Ratio_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(name: culture);

            SpeedIndex index = MakeSpeedIndex(entries: [(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 250)]
            );
            ScopedDecisionLog log = new();

            HardwareResolutionResult result = _resolver.Resolve(
                codec: VideoCodecType.H264,
                preference: HardwarePreference.ForceHardware,
                availableEncoders: WithNvenc(),
                speedIndex: index,
                decisions: log
            );

            Assert.Equal(expected: "h264_nvenc", actual: result.EncoderHandle);
            string message = log.Snapshot()[index: 0].Message;
            Assert.Contains(expectedSubstring: "2.5", actualString: message);
            Assert.DoesNotContain(expectedSubstring: "2,5", actualString: message);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
