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

/// <summary>
/// HolderBackedHardwareCapabilities is the forwarding wrapper that lets the DI
/// container hand out a singleton IHardwareCapabilities before
/// HardwareInitializationService has populated it. The contract: every read
/// goes through Holder.Current, with a CPU-only fallback when Current is null.
/// </summary>
public class HolderBackedHardwareCapabilitiesTests
{
    private static GpuDevice GpuFor(VideoCodecType codec) =>
        new(
            GpuVendor.Nvidia,
            "RTX 4080",
            16384,
            8,
            [codec]
        );

    [Fact]
    public void NullCurrent_GpusReturnsEmpty()
    {
        HardwareCapabilitiesHolder holder = new();
        HolderBackedHardwareCapabilities subject = new(holder);

        subject.Gpus.Should().BeEmpty();
        subject.HasGpu.Should().BeFalse();
    }

    [Fact]
    public void NullCurrent_CpuCoresFallsBackToEnvironmentProcessorCount()
    {
        HardwareCapabilitiesHolder holder = new();
        HolderBackedHardwareCapabilities subject = new(holder);

        subject.CpuCores.Should().Be(Environment.ProcessorCount);
    }

    [Fact]
    public void NullCurrent_SupportsHardwareEncodingReturnsFalse()
    {
        HardwareCapabilitiesHolder holder = new();
        HolderBackedHardwareCapabilities subject = new(holder);

        subject.SupportsHardwareEncoding(VideoCodecType.H264).Should().BeFalse();
    }

    [Fact]
    public void NullCurrent_GetGpuForCodecReturnsNull()
    {
        HardwareCapabilitiesHolder holder = new();
        HolderBackedHardwareCapabilities subject = new(holder);

        subject.GetGpuForCodec(VideoCodecType.H264).Should().BeNull();
    }

    [Fact]
    public void PopulatedCurrent_ForwardsGpuListThrough()
    {
        GpuDevice gpu = GpuFor(VideoCodecType.H265);
        HardwareCapabilitiesHolder holder = new()
        {
            Current = new HardwareCapabilities([gpu], 24),
        };
        HolderBackedHardwareCapabilities subject = new(holder);

        subject.Gpus.Should().ContainSingle().Which.Should().Be(gpu);
        subject.HasGpu.Should().BeTrue();
        subject.CpuCores.Should().Be(24);
    }

    [Fact]
    public void PopulatedCurrent_SupportsHardwareEncoding_RoutesToHeldCaps()
    {
        HardwareCapabilitiesHolder holder = new()
        {
            Current = new HardwareCapabilities([GpuFor(VideoCodecType.H265)], 8),
        };
        HolderBackedHardwareCapabilities subject = new(holder);

        subject.SupportsHardwareEncoding(VideoCodecType.H265).Should().BeTrue();
        subject.SupportsHardwareEncoding(VideoCodecType.Av1).Should().BeFalse();
    }

    [Fact]
    public void HolderUpdate_LiveReflectsInForwarder()
    {
        // This is the whole point of the indirection: post-detection update
        // must reach consumers that resolved the singleton pre-detection.
        HardwareCapabilitiesHolder holder = new();
        HolderBackedHardwareCapabilities subject = new(holder);

        subject.HasGpu.Should().BeFalse();

        holder.Current = new HardwareCapabilities([GpuFor(VideoCodecType.Av1)], 16);

        subject.HasGpu.Should().BeTrue();
        subject.Gpus.Should().ContainSingle();
        subject.SupportsHardwareEncoding(VideoCodecType.Av1).Should().BeTrue();
    }
}
