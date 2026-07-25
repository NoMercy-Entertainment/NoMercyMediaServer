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

using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

public class NvencSessionCapTests
{
    // ---- helpers -----------------------------------------------------------

    private static IHardwareCapabilities MakeHardware(params int[] capsPerGpu)
    {
        List<GpuDevice> gpus = [];

        foreach (int cap in capsPerGpu)
        {
            gpus.Add(
                new(
                    Vendor: GpuVendor.Nvidia,
                    Name: "Test GPU",
                    VramMb: 8192,
                    MaxEncoderSessions: cap,
                    SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265]
                )
            );
        }

        Mock<IHardwareCapabilities> mock = new();
        mock.Setup(h => h.Gpus).Returns(gpus);
        mock.Setup(h => h.HasGpu).Returns(gpus.Count > 0);
        return mock.Object;
    }

    private static IEncoderProcessRegistry MakeRegistry(int activeNvencSessions)
    {
        Mock<IEncoderProcessRegistry> mock = new();
        mock.Setup(r => r.CountConcurrentNvencSessions()).Returns(activeNvencSessions);
        return mock.Object;
    }

    // ---- tests -------------------------------------------------------------

    [Fact]
    public void EnforceForGpuEncode_does_nothing_when_below_cap()
    {
        // 0 active sessions, cap 3 — no throw
        NvencSessionCap cap = new(MakeHardware(3), MakeRegistry(0));

        Action act = () => cap.EnforceForGpuEncode("RTX 3080", requiresGpu: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnforceForGpuEncode_throws_GpuCapacityExhausted_when_at_cap()
    {
        // 3 active sessions, cap 3 — should throw 409 with correct rule id
        NvencSessionCap cap = new(MakeHardware(3), MakeRegistry(3));

        Action act = () => cap.EnforceForGpuEncode("RTX 3080", requiresGpu: true);

        EncoderRuntimeException ex = act.Should().Throw<EncoderRuntimeException>().Which;

        ex.HttpStatusCode.Should().Be(409);
        ex.Shape.Id.Should().Be(EncoderRuleId.GpuCapacityExhausted);
        ex.Shape.Suggestion.Should().Contain("force_software");
    }

    [Fact]
    public void EnforceForGpuEncode_skips_check_when_no_GPU_present()
    {
        // CPU-only system — no GPUs, cap check is meaningless
        NvencSessionCap cap = new(MakeHardware(), MakeRegistry(99));

        Action act = () => cap.EnforceForGpuEncode("(none)", requiresGpu: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnforceForGpuEncode_uses_lowest_cap_across_multiple_GPUs()
    {
        // Two GPUs: consumer cap 3, pro cap 8 — effective cap is 3
        NvencSessionCap cap = new(MakeHardware(3, 8), MakeRegistry(3));

        Action act = () => cap.EnforceForGpuEncode("Multi-GPU", requiresGpu: true);

        act.Should().Throw<EncoderRuntimeException>().Which.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void EnforceForGpuEncode_skips_check_when_requiresGpu_is_false()
    {
        // Saturated GPU but software encode was requested — no throw
        NvencSessionCap cap = new(MakeHardware(3), MakeRegistry(3));

        Action act = () => cap.EnforceForGpuEncode("RTX 3080", requiresGpu: false);

        act.Should().NotThrow();
    }
}
