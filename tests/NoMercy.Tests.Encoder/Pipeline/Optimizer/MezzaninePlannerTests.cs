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

using NoMercy.Encoder.Pipeline.Optimizer;

namespace NoMercy.Tests.Encoder.Pipeline.Optimizer;

public class MezzaninePlannerTests
{
    private const double Threshold = 50.0;

    [Fact]
    public void HeavyDistributedMultiRung_UsesMezzanine()
    {
        MezzaninePlanner
            .ShouldUseMezzanine(
                totalCost: 100,
                distributedEncodingEnabled: true,
                workerCount: 3,
                rungCount: 5,
                threshold: Threshold
            )
            .Should()
            .BeTrue();
    }

    [Fact]
    public void NotDistributed_NeverUsesMezzanine()
    {
        MezzaninePlanner
            .ShouldUseMezzanine(1000, distributedEncodingEnabled: false, 8, 6, Threshold)
            .Should()
            .BeFalse("single-box jobs derive in-process — no extra full-res write");
    }

    [Fact]
    public void SingleWorker_NeverUsesMezzanine()
    {
        MezzaninePlanner.ShouldUseMezzanine(1000, true, 1, 6, Threshold).Should().BeFalse();
    }

    [Fact]
    public void SingleRung_NothingToAmortise_False()
    {
        MezzaninePlanner.ShouldUseMezzanine(1000, true, 4, 1, Threshold).Should().BeFalse();
    }

    [Fact]
    public void LightJob_BelowThreshold_False()
    {
        MezzaninePlanner.ShouldUseMezzanine(10, true, 4, 5, Threshold).Should().BeFalse();
    }
}
