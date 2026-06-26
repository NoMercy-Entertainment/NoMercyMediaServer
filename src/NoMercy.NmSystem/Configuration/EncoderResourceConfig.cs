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

namespace NoMercy.NmSystem.Configuration;

public class EncoderResourceConfig
{
    public double EncoderCpuHeadroomPercent { get; set; } = 90.0;
    public double EncoderGpuHeadroomPercent { get; set; } = 95.0;
    public long EncoderMinFreeMemoryMb { get; set; } = 1024;
}
