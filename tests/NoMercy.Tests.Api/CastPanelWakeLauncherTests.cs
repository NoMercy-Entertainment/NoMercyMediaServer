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
[Trait("Category", "Unit")]
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
            chromeCast.Object,
            NullLogger<CastPanelWakeLauncher>.Instance
        );
        return (launcher, chromeCast);
    }

    [Theory]
    [InlineData([true, false])]
    [InlineData([false, true])]
    public void ShouldFireCastWake_ReflectsInverseOfTargetIsLive(bool targetIsLive, bool expected)
    {
        CastPanelWakeLauncher.ShouldFireCastWake(targetIsLive).Should().Be(expected);
    }

    [Fact]
    public async Task LaunchIfColdAsync_NeverTouchesChromeCast_WhenTargetIsLive()
    {
        (CastPanelWakeLauncher launcher, Mock<IChromeCastService> chromeCast) = MakeLauncher();
        bool resolveLaunchDataCalled = false;

        await launcher.LaunchIfColdAsync(
            true,
            TargetIp,
            true,
            () =>
            {
                resolveLaunchDataCalled = true;
                return Task.FromResult<LaunchCustomData?>(null);
            }
        );

        resolveLaunchDataCalled
            .Should()
            .BeFalse("a live target must never pay for a token-exchange call it would discard");
        chromeCast.Verify(c => c.FindReceiverNameByIpAsync(It.IsAny<string>()), Times.Never);
        chromeCast.Verify(c => c.SelectChromecast(It.IsAny<string>()), Times.Never);
        chromeCast.Verify(
            c =>
                c.LaunchAndroidReceiver(It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<bool>()),
            Times.Never
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LaunchIfColdAsync_InvokesSelectAndLaunch_WhenTargetIsNotLive(bool apkOnline)
    {
        (CastPanelWakeLauncher launcher, Mock<IChromeCastService> chromeCast) = MakeLauncher();
        LaunchCustomData launchData = new() { AccessToken = "token" };
        chromeCast.Setup(c => c.FindReceiverNameByIpAsync(TargetIp)).ReturnsAsync(ReceiverName);

        await launcher.LaunchIfColdAsync(
            false,
            TargetIp,
            apkOnline,
            () => Task.FromResult<LaunchCustomData?>(launchData)
        );

        chromeCast.Verify(c => c.SelectChromecast(ReceiverName), Times.Once);
        chromeCast.Verify(
            c => c.LaunchAndroidReceiver(ReceiverName, launchData, apkOnline),
            Times.Once
        );
    }

    [Fact]
    public async Task LaunchIfColdAsync_ResolvesLaunchData_OnlyWhenTargetIsNotLive()
    {
        (CastPanelWakeLauncher launcher, Mock<IChromeCastService> chromeCast) = MakeLauncher();
        chromeCast.Setup(c => c.FindReceiverNameByIpAsync(TargetIp)).ReturnsAsync(ReceiverName);
        int resolveCallCount = 0;

        await launcher.LaunchIfColdAsync(
            false,
            TargetIp,
            true,
            () =>
            {
                resolveCallCount++;
                return Task.FromResult<LaunchCustomData?>(null);
            }
        );

        resolveCallCount.Should().Be(1);
    }

    [Fact]
    public async Task LaunchIfColdAsync_SkipsLaunch_WhenNoReceiverDiscoveredAtTargetIp()
    {
        (CastPanelWakeLauncher launcher, Mock<IChromeCastService> chromeCast) = MakeLauncher();
        chromeCast.Setup(c => c.FindReceiverNameByIpAsync(TargetIp)).ReturnsAsync((string?)null);

        await launcher.LaunchIfColdAsync(
            false,
            TargetIp,
            true,
            () => Task.FromResult<LaunchCustomData?>(null)
        );

        chromeCast.Verify(c => c.SelectChromecast(It.IsAny<string>()), Times.Never);
        chromeCast.Verify(
            c =>
                c.LaunchAndroidReceiver(It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<bool>()),
            Times.Never
        );
    }
}
