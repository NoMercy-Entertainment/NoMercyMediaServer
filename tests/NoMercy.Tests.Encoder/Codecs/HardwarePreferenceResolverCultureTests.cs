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
            SpeedKey key = new(codec, encoder, 1920, null);
            dict[key] = new(fps, 1.0, DateTime.UtcNow);
        }

        return new(dict);
    }

    private static List<string> WithNvenc() =>
        ["libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc"];

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("fr-FR")]
    public void PreferHardware_Ratio_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            // 250 / 100 = 2.5x exactly — clean single-decimal ratio.
            SpeedIndex index = MakeSpeedIndex([(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 250)]
            );
            ScopedDecisionLog log = new();

            HardwareResolutionResult result = _resolver.Resolve(
                VideoCodecType.H264,
                HardwarePreference.PreferHardware,
                WithNvenc(),
                index,
                log
            );

            Assert.Equal("h264_nvenc", result.EncoderHandle);
            string message = log.Snapshot()[0].Message;
            Assert.Contains("2.5", message);
            Assert.DoesNotContain("2,5", message);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("nl-NL")]
    [InlineData("fr-FR")]
    public void ForceHardware_Ratio_StaysPeriodDecimalUnderCommaCulture(string culture)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(culture);

            SpeedIndex index = MakeSpeedIndex([(VideoCodecType.H264, "libx264", 100), (VideoCodecType.H264, "h264_nvenc", 250)]
            );
            ScopedDecisionLog log = new();

            HardwareResolutionResult result = _resolver.Resolve(
                VideoCodecType.H264,
                HardwarePreference.ForceHardware,
                WithNvenc(),
                index,
                log
            );

            Assert.Equal("h264_nvenc", result.EncoderHandle);
            string message = log.Snapshot()[0].Message;
            Assert.Contains("2.5", message);
            Assert.DoesNotContain("2,5", message);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
