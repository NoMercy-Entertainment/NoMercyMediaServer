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

public class ProcessResourceMonitorTests
{
    [Fact]
    public void GetCpuUsagePercent_FirstCall_ReturnsNonNegativeValue()
    {
        ProcessResourceMonitor sut = new();

        double percent = sut.GetCpuUsagePercent();

        percent.Should().BeGreaterThanOrEqualTo(0);
        percent.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void GetCpuUsagePercent_SecondCall_ReturnsRelativeValue()
    {
        ProcessResourceMonitor sut = new();
        // Prime the snapshot cache.
        sut.GetCpuUsagePercent();
        Thread.Sleep(10);

        double percent = sut.GetCpuUsagePercent();

        percent.Should().BeGreaterThanOrEqualTo(0);
        percent.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void GetAvailableMemoryMb_ReturnsNonNegative()
    {
        ProcessResourceMonitor sut = new();

        long mb = sut.GetAvailableMemoryMb();

        mb.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GetGpuEncodeUtilization_IsZero_WithoutVendorPlugin()
    {
        ProcessResourceMonitor sut = new();
        GpuDevice nvidia = new(
            Vendor: GpuVendor.Nvidia,
            Name: "RTX 4080",
            VramMb: 16_384,
            MaxEncoderSessions: 12,
            SupportedCodecs: [VideoCodecType.H264]
        );

        double util = sut.GetGpuEncodeUtilization(nvidia.Name);

        util.Should().Be(0.0);
    }

    [Fact]
    public void NullResourceMonitor_ReturnsZeros()
    {
        NullResourceMonitor sut = new();

        sut.GetCpuUsagePercent().Should().Be(0);
        sut.GetSystemCpuUsagePercent().Should().Be(0);
        sut.GetAvailableMemoryMb().Should().Be(0);
        sut.GetGpuEncodeUtilization("n/a").Should().Be(0);
    }

    [Fact]
    public void GetSystemCpuUsagePercent_FirstCall_ReturnsClampedValue()
    {
        ProcessResourceMonitor sut = new();

        double percent = sut.GetSystemCpuUsagePercent();

        percent.Should().BeGreaterThanOrEqualTo(0);
        percent.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void GetSystemCpuUsagePercent_SecondCall_AfterDelay_ReturnsClampedValue()
    {
        ProcessResourceMonitor sut = new();
        sut.GetSystemCpuUsagePercent();
        Thread.Sleep(50);

        double percent = sut.GetSystemCpuUsagePercent();

        percent.Should().BeGreaterThanOrEqualTo(0);
        percent.Should().BeLessThanOrEqualTo(100);
        double.IsNaN(percent).Should().BeFalse();
    }
}
