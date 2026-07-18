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
/// Bundle-cap resolution. The cap is a HARD concurrency limit — how many
/// encode sessions may share ONE ffmpeg (one decode) — not a throughput knob.
/// For the GPU that is the driver's concurrent NVENC session limit; for the
/// CPU it is core-bounded. Rungs off one source share a single hoisted
/// decode/crop, so splitting them is never a throughput win (it only
/// re-decodes) and only running out of sessions forces a split.
/// </summary>
public class BundleCapResolverTests
{
    private static GpuDevice MakeGpu(string name = "RTX 4080", int maxSessions = 8) =>
        new(
            Vendor: GpuVendor.Nvidia,
            Name: name,
            VramMb: 16384,
            MaxEncoderSessions: maxSessions,
            SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );

    private static IHardwareCapabilities MakeHardware(GpuDevice? gpu, int cpuCores = 16) =>
        new HardwareCapabilities(gpu is null ? [] : [gpu], cpuCores);

    // ── GPU cap = driver concurrent-session limit ───────────────────────────

    [Fact]
    public void Resolve_ConsumerCard_GpuCapIsTheDriverSessionLimit()
    {
        // RTX 2080 SUPER reports 8 concurrent NVENC sessions — every rung of a
        // realistic ladder shares one decode up to that limit.
        IHardwareCapabilities hw = MakeHardware(MakeGpu("RTX 2080 SUPER", maxSessions: 8));
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 3840, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, hw);

        gpuCap.Should().Be(8);
    }

    [Fact]
    public void Resolve_LowerSessionLimit_IsRespected()
    {
        // Older consumer cards cap at 3 concurrent sessions — a 5-rung ladder
        // physically cannot open more than 3 in one ffmpeg.
        IHardwareCapabilities hw = MakeHardware(MakeGpu(maxSessions: 3));
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, hw);

        gpuCap.Should().Be(3);
    }

    [Fact]
    public void Resolve_UnlimitedDriverCap_UsesPracticalCeiling()
    {
        // Professional / patched-driver cards report int.MaxValue. A single
        // bundle must not grow unbounded, but every realistic ladder still
        // stays together under the practical ceiling.
        IHardwareCapabilities hw = MakeHardware(MakeGpu("L40", maxSessions: int.MaxValue));
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, hw);

        gpuCap.Should().Be(32); // UnlimitedGpuBundleCap
    }

    [Fact]
    public void Resolve_NoGpu_GpuCapFallsBackToPracticalCeiling()
    {
        // No GPU means no GPU rungs to chunk; the cap is irrelevant but must
        // never be a fragmenting value.
        IHardwareCapabilities hw = MakeHardware(gpu: null, cpuCores: 16);
        BundleCapResolver.PlannedRung[] rungs = [Rung("libx265", 1920, isGpu: false)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, hw);

        gpuCap.Should().Be(32);
    }

    [Fact]
    public void Resolve_NullHardware_GpuCapFallsBackToPracticalCeiling()
    {
        BundleCapResolver.PlannedRung[] rungs = [Rung("hevc_nvenc", 1920, isGpu: true)];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, hardware: null);

        gpuCap.Should().Be(32);
    }

    // ── The regression: a 4K + 1080p ladder must NOT fragment ───────────────

    [Fact]
    public void Resolve_MultiRung4KLadder_OnConsumerCard_DoesNotFragmentSharedDecode()
    {
        // The reported bug: the old throughput model returned
        // floor(4K-hevc-realtime / 1.5) — which on a 2080 SUPER is 1 — so the
        // 4K-HDR master and the 1080p-SDR rung derived from it were split into
        // two ffmpegs, the 1080p re-cropping the source. The cap must be the
        // card's 8-session limit, keeping both rungs in one shared decode.
        IHardwareCapabilities hw = MakeHardware(MakeGpu("RTX 2080 SUPER", maxSessions: 8));
        BundleCapResolver.PlannedRung[] rungs =
        [
            Rung("hevc_nvenc", 3840, isGpu: true), // 4K HDR master (slowest rung)
            Rung("hevc_nvenc", 1920, isGpu: true), // 1080p SDR, derived
        ];

        (int gpuCap, _) = BundleCapResolver.Resolve(rungs, hw);

        gpuCap
            .Should()
            .Be(8)
            .And.BeGreaterThanOrEqualTo(
                rungs.Length,
                "both rungs share one hoisted decode/crop and must fit one bundle"
            );
    }

    // ── CPU cap = core-bounded ──────────────────────────────────────────────

    [Fact]
    public void Resolve_CpuCap_IsCoreBounded()
    {
        // 32 cores / 2 minimum threads per software encode = 16.
        IHardwareCapabilities hw = MakeHardware(MakeGpu(), cpuCores: 32);
        BundleCapResolver.PlannedRung[] rungs = [Rung("libx265", 1920, isGpu: false)];

        (_, int cpuCap) = BundleCapResolver.Resolve(rungs, hw);

        cpuCap.Should().Be(16);
    }

    [Fact]
    public void Resolve_CpuCap_SmallHost_StillAtLeastOne()
    {
        IHardwareCapabilities hw = MakeHardware(MakeGpu(), cpuCores: 1);
        BundleCapResolver.PlannedRung[] rungs = [Rung("libx265", 1920, isGpu: false)];

        (_, int cpuCap) = BundleCapResolver.Resolve(rungs, hw);

        cpuCap.Should().Be(1);
    }

    [Fact]
    public void Resolve_CpuCap_UnknownCores_FallsBackToOne()
    {
        BundleCapResolver.PlannedRung[] rungs = [Rung("libx265", 1920, isGpu: false)];

        (_, int cpuCap) = BundleCapResolver.Resolve(rungs, hardware: null);

        cpuCap.Should().Be(1); // UnknownCpuCapFallback
    }

    private static BundleCapResolver.PlannedRung Rung(string encoder, int width, bool isGpu)
    {
        VideoCodecType codec =
            encoder.Contains("hevc") || encoder.Contains("x265") ? VideoCodecType.H265
            : encoder.Contains("av1") ? VideoCodecType.Av1
            : encoder.Contains("vp9") ? VideoCodecType.Vp9
            : VideoCodecType.H264;
        return new(Codec: codec, EncoderName: encoder, Width: width, IsGpu: isGpu);
    }
}
