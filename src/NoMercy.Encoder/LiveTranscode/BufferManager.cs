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

namespace NoMercy.Encoder.LiveTranscode;

public enum BufferAction
{
    None,
    Suspend,
    Resume,
    DropQuality,
    EmergencyDropQuality,
}

public class BufferManager(LiveSessionLimits limits)
{
    public BufferAction Evaluate(TimeSpan bufferAhead, bool isSuspended)
    {
        double seconds = bufferAhead.TotalSeconds;
        BufferThresholds t = limits.Buffer;

        if (seconds > t.SuspendAboveSeconds && !isSuspended)
            return BufferAction.Suspend;

        if (seconds < t.ResumeBelowSeconds && isSuspended)
            return BufferAction.Resume;

        if (seconds < t.EmergencyDropBelowSeconds)
            return BufferAction.EmergencyDropQuality;

        if (seconds < t.DropQualityBelowSeconds)
            return BufferAction.DropQuality;

        return BufferAction.None;
    }
}
