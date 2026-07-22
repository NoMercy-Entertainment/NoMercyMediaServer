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
using NoMercy.Api.Services.Music;
using NoMercy.Networking.Cast;
using NoMercy.Setup.Cast;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Regression pin for MusicHub.ChangeDeviceCommand's server-side Cast
/// panel-wake LAUNCH. Commit 9804a201 fired this LAUNCH (SelectChromecast +
/// LaunchAndroidReceiver via IChromeCastService) for every TV-target
/// ChangeDevice, dropping the liveness gate; cast_shell then missed the
/// running APK on the Cast Connect path, fell back to the Web Receiver over
/// the playing native app, and the liveness sweep force-ended the session
/// ~15s later. 374b57d5 restored the gate inside what is now
/// CastPanelWakeLauncher. These tests go red if that gate is ever dropped
/// again, regardless of what MusicHub itself does around it.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class CastPanelWakeLauncherTests
{
    private const string TargetIp = "192.168.1.50";
    private const string ReceiverName = "Living Room TV";

    private static (
        CastPanelWakeLauncher Launcher,
        Mock<IChromeCastService> ChromeCast
    ) MakeLauncher()
    {
        Mock<IChromeCastService> chromeCast = new();
        CastPanelWakeLauncher launcher = new(
            chromeCast: chromeCast.Object,
            logger: NullLogger<CastPanelWakeLauncher>.Instance
        );
        return (launcher, chromeCast);
    }

    [Theory]
    [InlineData(data: [true, false])]
    [InlineData(data: [false, true])]
    public void ShouldFireCastWake_ReflectsInverseOfTargetIsLive(bool targetIsLive, bool expected)
    {
        CastPanelWakeLauncher.ShouldFireCastWake(targetIsLive: targetIsLive).Should().Be(expected: expected);
    }

    [Fact]
    public async Task LaunchIfColdAsync_NeverTouchesChromeCast_WhenTargetIsLive()
    {
        (CastPanelWakeLauncher launcher, Mock<IChromeCastService> chromeCast) = MakeLauncher();
        bool resolveLaunchDataCalled = false;

        await launcher.LaunchIfColdAsync(
            targetIsLive: true,
            targetIp: TargetIp,
            useAndroidReceiver: true,
            resolveLaunchData: () =>
            {
                resolveLaunchDataCalled = true;
                return Task.FromResult<LaunchCustomData?>(result: null);
            }
        );

        resolveLaunchDataCalled
            .Should()
            .BeFalse(because: "a live target must never pay for a token-exchange call it would discard");
        chromeCast.Verify(expression: c => c.FindReceiverNameByIpAsync(It.IsAny<string>()), times: Times.Never);
        chromeCast.Verify(expression: c => c.SelectChromecast(It.IsAny<string>()), times: Times.Never);
        chromeCast.Verify(
            expression: c =>
                c.LaunchAndroidReceiver(It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<bool>()),
            times: Times.Never
        );
    }

    [Theory]
    [InlineData(data: true)]
    [InlineData(data: false)]
    public async Task LaunchIfColdAsync_InvokesSelectAndLaunch_WhenTargetIsNotLive(bool apkOnline)
    {
        (CastPanelWakeLauncher launcher, Mock<IChromeCastService> chromeCast) = MakeLauncher();
        LaunchCustomData launchData = new() { AccessToken = "token" };
        chromeCast.Setup(expression: c => c.FindReceiverNameByIpAsync(TargetIp)).ReturnsAsync(value: ReceiverName);

        await launcher.LaunchIfColdAsync(
            targetIsLive: false,
            targetIp: TargetIp,
            useAndroidReceiver: apkOnline,
            resolveLaunchData: () => Task.FromResult<LaunchCustomData?>(result: launchData)
        );

        chromeCast.Verify(expression: c => c.SelectChromecast(ReceiverName), times: Times.Once);
        chromeCast.Verify(
            expression: c => c.LaunchAndroidReceiver(ReceiverName, launchData, apkOnline),
            times: Times.Once
        );
    }

    [Fact]
    public async Task LaunchIfColdAsync_ResolvesLaunchData_OnlyWhenTargetIsNotLive()
    {
        (CastPanelWakeLauncher launcher, Mock<IChromeCastService> chromeCast) = MakeLauncher();
        chromeCast.Setup(expression: c => c.FindReceiverNameByIpAsync(TargetIp)).ReturnsAsync(value: ReceiverName);
        int resolveCallCount = 0;

        await launcher.LaunchIfColdAsync(
            targetIsLive: false,
            targetIp: TargetIp,
            useAndroidReceiver: true,
            resolveLaunchData: () =>
            {
                resolveCallCount++;
                return Task.FromResult<LaunchCustomData?>(result: null);
            }
        );

        resolveCallCount.Should().Be(expected: 1);
    }

    [Fact]
    public async Task LaunchIfColdAsync_SkipsLaunch_WhenNoReceiverDiscoveredAtTargetIp()
    {
        (CastPanelWakeLauncher launcher, Mock<IChromeCastService> chromeCast) = MakeLauncher();
        chromeCast.Setup(expression: c => c.FindReceiverNameByIpAsync(TargetIp)).ReturnsAsync(value: (string?)null);

        await launcher.LaunchIfColdAsync(
            targetIsLive: false,
            targetIp: TargetIp,
            useAndroidReceiver: true,
            resolveLaunchData: () => Task.FromResult<LaunchCustomData?>(result: null)
        );

        chromeCast.Verify(expression: c => c.SelectChromecast(It.IsAny<string>()), times: Times.Never);
        chromeCast.Verify(
            expression: c =>
                c.LaunchAndroidReceiver(It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<bool>()),
            times: Times.Never
        );
    }
}
