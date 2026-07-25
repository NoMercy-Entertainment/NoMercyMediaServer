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
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.OpticalMedia.Capabilities;

namespace NoMercy.Tests.OpticalMedia.Capabilities;

/// <summary>
/// REQUIREMENT: <see cref="FfmpegBluRayCapability"/> probes the bundled
/// ffmpeg for libbluray/AACS support by running <c>ffmpeg -protocols</c> and
/// checking the combined stdout+stderr for the "bluray" token. It must also
/// resolve which KEYDB.cfg is active — an operator override when configured,
/// otherwise the bundled default — without ever throwing (a missing
/// Blu-ray capability must degrade, not crash startup).
/// </summary>
[Trait("Category", "Unit")]
public class FfmpegBluRayCapabilityTests
{
    private static EncoderOptions MakeOptions(BluRayOptions? bluRay = null) =>
        new() { FfmpegPathOverride = "ffmpeg", BluRay = bluRay };

    private static Mock<IProcessRunner> MakeRunner(
        string stdOut = "",
        string stdErr = "",
        int exitCode = 0
    )
    {
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
            .ReturnsAsync(
                new ProcessResult(exitCode, stdOut, stdErr, TimeSpan.FromMilliseconds(10))
            );
        return runner;
    }

    [Fact]
    public async Task ProbeAsync_StdoutContainsBluray_SetsProtocolPresentTrue()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdOut: "file,http,bluray,rtmp");
        FfmpegBluRayCapability capability = new(
            MakeOptions(),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        await capability.ProbeAsync();

        capability.BluRayProtocolPresent.Should().BeTrue();
    }

    [Fact]
    public async Task ProbeAsync_StderrContainsBluray_SetsProtocolPresentTrue()
    {
        // ffmpeg -protocols can exit non-zero yet still write the protocol
        // list to stderr depending on the build — both streams are checked.
        Mock<IProcessRunner> runner = MakeRunner(stdErr: "bluray: supported");
        FfmpegBluRayCapability capability = new(
            MakeOptions(),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        await capability.ProbeAsync();

        capability.BluRayProtocolPresent.Should().BeTrue();
    }

    [Fact]
    public async Task ProbeAsync_NeitherStreamContainsBluray_SetsProtocolPresentFalse()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdOut: "file,http,rtmp");
        FfmpegBluRayCapability capability = new(
            MakeOptions(),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        await capability.ProbeAsync();

        capability.BluRayProtocolPresent.Should().BeFalse();
    }

    [Fact]
    public async Task ProbeAsync_BlurayCheckIsCaseInsensitive()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdOut: "FILE,BLURAY,RTMP");
        FfmpegBluRayCapability capability = new(
            MakeOptions(),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        await capability.ProbeAsync();

        capability.BluRayProtocolPresent.Should().BeTrue();
    }

    [Fact]
    public async Task ProbeAsync_NoKeyDbOverride_ReportsBundledDefault()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdOut: "bluray");
        FfmpegBluRayCapability capability = new(
            MakeOptions(bluRay: null),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        await capability.ProbeAsync();

        capability.ActiveKeyDbPath.Should().Be("bundled (nomercy-ffmpeg default)");
    }

    [Fact]
    public async Task ProbeAsync_KeyDbOverrideConfigured_ReportsOverridePath()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdOut: "bluray");
        BluRayOptions bluRay = new() { KeyDbOverridePath = "/etc/nomercy/KEYDB.cfg" };
        FfmpegBluRayCapability capability = new(
            MakeOptions(bluRay),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        await capability.ProbeAsync();

        capability.ActiveKeyDbPath.Should().Be("override:/etc/nomercy/KEYDB.cfg");
    }

    [Fact]
    public async Task ProbeAsync_WhitespaceKeyDbOverride_FallsBackToBundledDefault()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdOut: "bluray");
        BluRayOptions bluRay = new() { KeyDbOverridePath = "   " };
        FfmpegBluRayCapability capability = new(
            MakeOptions(bluRay),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        await capability.ProbeAsync();

        capability.ActiveKeyDbPath.Should().Be("bundled (nomercy-ffmpeg default)");
    }

    [Fact]
    public async Task ProbeAsync_AacsKeysOverrideConfigured_DoesNotThrow()
    {
        // AacsKeysOverridePath only drives an extra log line; assert the
        // no-throw contract rather than the log text itself.
        Mock<IProcessRunner> runner = MakeRunner(stdOut: "bluray");
        BluRayOptions bluRay = new() { AacsKeysOverridePath = "/etc/nomercy/bdplus" };
        FfmpegBluRayCapability capability = new(
            MakeOptions(bluRay),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        Func<Task> act = () => capability.ProbeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProbeAsync_BeforeCalled_PropertiesHaveSafeDefaults()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdOut: "bluray");
        FfmpegBluRayCapability capability = new(
            MakeOptions(),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        capability.BluRayProtocolPresent.Should().BeFalse();
        capability.ActiveKeyDbPath.Should().BeEmpty();
    }

    [Fact]
    public async Task ProbeAsync_PassesHideBannerAndProtocolsFlags()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdOut: "bluray");
        FfmpegBluRayCapability capability = new(
            MakeOptions(),
            runner.Object,
            NullLogger<FfmpegBluRayCapability>.Instance
        );

        await capability.ProbeAsync();

        runner.Verify(
            r =>
                r.RunAsync(
                    "ffmpeg",
                    It.Is<string[]>(args =>
                        args.Contains("-hide_banner") && args.Contains("-protocols")
                    ),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
