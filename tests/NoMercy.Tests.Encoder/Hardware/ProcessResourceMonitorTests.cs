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

        percent.Should().BeGreaterThanOrEqualTo(expected: 0);
        percent.Should().BeLessThanOrEqualTo(expected: 100);
    }

    [Fact]
    public void GetCpuUsagePercent_SecondCall_ReturnsRelativeValue()
    {
        ProcessResourceMonitor sut = new();
        // Prime the snapshot cache.
        sut.GetCpuUsagePercent();
        Thread.Sleep(millisecondsTimeout: 10);

        double percent = sut.GetCpuUsagePercent();

        percent.Should().BeGreaterThanOrEqualTo(expected: 0);
        percent.Should().BeLessThanOrEqualTo(expected: 100);
    }

    [Fact]
    public void GetAvailableMemoryMb_ReturnsNonNegative()
    {
        ProcessResourceMonitor sut = new();

        long mb = sut.GetAvailableMemoryMb();

        mb.Should().BeGreaterThanOrEqualTo(expected: 0);
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

        double util = sut.GetGpuEncodeUtilization(gpuDeviceKey: nvidia.Name);

        util.Should().Be(expected: 0.0);
    }

    [Fact]
    public void NullResourceMonitor_ReturnsZeros()
    {
        NullResourceMonitor sut = new();

        sut.GetCpuUsagePercent().Should().Be(expected: 0);
        sut.GetSystemCpuUsagePercent().Should().Be(expected: 0);
        sut.GetAvailableMemoryMb().Should().Be(expected: 0);
        sut.GetGpuEncodeUtilization(gpuDeviceKey: "n/a").Should().Be(expected: 0);
    }

    [Fact]
    public void GetSystemCpuUsagePercent_FirstCall_ReturnsClampedValue()
    {
        ProcessResourceMonitor sut = new();

        double percent = sut.GetSystemCpuUsagePercent();

        percent.Should().BeGreaterThanOrEqualTo(expected: 0);
        percent.Should().BeLessThanOrEqualTo(expected: 100);
    }

    [Fact]
    public void GetSystemCpuUsagePercent_SecondCall_AfterDelay_ReturnsClampedValue()
    {
        ProcessResourceMonitor sut = new();
        sut.GetSystemCpuUsagePercent();
        Thread.Sleep(millisecondsTimeout: 50);

        double percent = sut.GetSystemCpuUsagePercent();

        percent.Should().BeGreaterThanOrEqualTo(expected: 0);
        percent.Should().BeLessThanOrEqualTo(expected: 100);
        double.IsNaN(d: percent).Should().BeFalse();
    }
}
