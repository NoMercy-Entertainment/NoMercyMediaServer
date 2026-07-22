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

namespace NoMercy.Encoder.Progress;

public class ProgressAggregator
{
    private readonly double[] _weights;
    private readonly double[] _groupProgress;

    public ProgressAggregator(TimeSpan[] estimatedDurations)
    {
        _weights = estimatedDurations.Select(selector: d => d.TotalSeconds).ToArray();
        _groupProgress = new double[_weights.Length];
        OverallPercentage = _weights.Sum();
    }

    public void UpdateGroup(int groupIndex, double percentage)
    {
        if (groupIndex >= 0 && groupIndex < _groupProgress.Length)
            _groupProgress[groupIndex] = Math.Clamp(value: percentage, min: 0, max: 100);
    }

    public double OverallPercentage
    {
        get
        {
            if (field <= 0)
                return 0;
            double weighted = 0;
            for (int i = 0; i < _groupProgress.Length; i++)
                weighted += _groupProgress[i] * _weights[i];
            return weighted / field;
        }
    }

    public TimeSpan? EstimatedRemaining(TimeSpan elapsed)
    {
        double percent = OverallPercentage;
        if (percent <= 0)
            return null;
        double totalEstimatedSeconds = elapsed.TotalSeconds / (percent / 100.0);
        double remainingSeconds = totalEstimatedSeconds - elapsed.TotalSeconds;
        return remainingSeconds > 0 ? TimeSpan.FromSeconds(value: remainingSeconds) : TimeSpan.Zero;
    }
}
