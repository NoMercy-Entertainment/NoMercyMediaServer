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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// Both capability probes feed PlanStage's encoder candidate list. When that
/// list loses its hardware entries the plan resolves libx265 instead of
/// hevc_nvenc, silently, and the choice is frozen into the queue payload — so
/// a probe that failed to reach an answer outlives the moment it failed.
/// <para>
/// This is not hypothetical: 81 identical 1080p HEVC 10-bit sources were queued
/// to software while 165 of their own siblings, same preset and same folder,
/// went to hevc_nvenc.
/// </para>
/// </summary>
public class CapabilityProbeSilentDowngradeTests
{
    private const string EncoderList = """
        Encoders:
         V..... libx265              libx265 H.265 / HEVC (codec hevc)
         V..... hevc_nvenc           NVIDIA NVENC hevc encoder (codec hevc)
        """;

    [Fact]
    public async Task AnEmptyReprobeDoesNotEraseTheEncodersAlreadyKnown()
    {
        Mock<IProcessRunner> runner = new();
        string encoders = EncoderList;

        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.Is<string[]>(args => args.Length == 1 && args[0] == "-encoders"),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() => new ProcessResult(0, encoders, "", TimeSpan.Zero));

        foreach (string flag in new[] { "-decoders", "-demuxers", "-filters", "-protocols" })
        {
            string captured = flag;
            runner
                .Setup(r =>
                    r.RunAsync(
                        It.IsAny<string>(),
                        It.Is<string[]>(args => args.Length == 1 && args[0] == captured),
                        null,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero));
        }

        FfmpegCapabilities capabilities = new(runner.Object);
        await capabilities.ProbeAsync();
        capabilities.HasEncoder("hevc_nvenc").Should().BeTrue("the first probe read the list");

        // The runtime re-probe: ffmpeg exits 0 and prints nothing, which the
        // parser cannot distinguish from a host with no encoders at all.
        encoders = string.Empty;
        await capabilities.ProbeAsync();

        capabilities
            .HasEncoder("hevc_nvenc")
            .Should()
            .BeTrue(
                "a probe that parsed to nothing has learned nothing — accepting it reports this "
                    + "host as software-only and every encode planned afterwards resolves libx265"
            );
    }

    [Fact]
    public async Task AnInitProbeThatTimesOutIsAskedAgainRatherThanBelieved()
    {
        int calls = 0;
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string[], string?, CancellationToken>(
                (_, _, _, _) =>
                {
                    calls++;
                    // The host is saturated by software encodes, so the first
                    // spawn never reaches a verdict inside the allowance.
                    if (calls == 1)
                        throw new OperationCanceledException();

                    return Task.FromResult(new ProcessResult(0, "", "", TimeSpan.Zero));
                }
            );

        HardwareEncoderProbe probe = new(runner.Object, NullLogger<HardwareEncoderProbe>.Instance);

        IReadOnlySet<string> usable = await probe.ProbeAsync(["hevc_nvenc"]);

        usable
            .Should()
            .Contain(
                "hevc_nvenc",
                "a spawn that never answered says nothing about the encoder, and condemning it "
                    + "pins the whole run to software — which loads the host further and makes "
                    + "the next probe more likely to time out too"
            );
        calls.Should().Be(2, "the inconclusive attempt is retried, not repeated forever");
    }

    [Fact]
    public async Task AnEncoderFfmpegActuallyRefusesIsNotRetried()
    {
        int calls = 0;
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, string[], string?, CancellationToken>(
                (_, _, _, _) =>
                {
                    calls++;
                    return Task.FromResult(
                        new ProcessResult(1, "", "Cannot load libcuda.so.1", TimeSpan.Zero)
                    );
                }
            );

        HardwareEncoderProbe probe = new(runner.Object, NullLogger<HardwareEncoderProbe>.Instance);

        IReadOnlySet<string> usable = await probe.ProbeAsync(["hevc_nvenc"]);

        usable.Should().BeEmpty();
        calls.Should().Be(1, "ffmpeg answered — a refusal is an answer and needs no second ask");
    }
}
