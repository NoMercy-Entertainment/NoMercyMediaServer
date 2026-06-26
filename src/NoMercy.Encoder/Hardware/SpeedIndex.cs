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

namespace NoMercy.Encoder.Hardware;

public record SpeedKey(VideoCodecType Codec, string Encoder, int Width, string? DeviceName);

public record SpeedMeasurement(double Fps, double SpeedMultiplier, DateTime MeasuredAt);

public record SpeedIndex(Dictionary<SpeedKey, SpeedMeasurement> Measurements)
{
    public SpeedMeasurement? GetSpeed(
        VideoCodecType codec,
        string encoder,
        int width,
        string? deviceName
    )
    {
        SpeedKey key = new(codec, encoder, width, deviceName);
        return Measurements.GetValueOrDefault(key);
    }

    public double GetSpeedMultiplier(
        VideoCodecType codec,
        string encoder,
        int width,
        string? deviceName
    )
    {
        SpeedMeasurement? measurement = GetSpeed(codec, encoder, width, deviceName);
        return measurement?.SpeedMultiplier ?? 0;
    }
}
