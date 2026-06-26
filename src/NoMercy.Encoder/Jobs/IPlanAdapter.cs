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

using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline.Stages;

namespace NoMercy.Encoder.Jobs;

public interface IPlanAdapter
{
    ExecutionPlan AdaptForLocalHardware(
        ExecutionPlan originalPlan,
        IHardwareCapabilities localHardware,
        SpeedIndex localSpeeds
    );
}
