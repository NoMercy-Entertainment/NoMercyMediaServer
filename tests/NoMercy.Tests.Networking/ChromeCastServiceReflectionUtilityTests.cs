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

using System.Reflection;
using System.Timers;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Discovery;
using Sharpcaster;
using Xunit;
using Timer = System.Timers.Timer;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: Sharpcaster's built-in heartbeat must be fully neutered
/// process-wide (see the class-level comment on DisableSharpcasterHeartbeat)
/// or an async-void SocketException on disconnect kills the whole server
/// process. NeutralizeTimer must stop the timer, disable AutoReset, and swap
/// its private _onIntervalElapsed delegate to a no-op — all without
/// disposing it (Sharpcaster later flips Enabled=true on a PONG, which
/// throws on a disposed timer). These exercise the real reflection-based
/// logic against a real System.Timers.Timer and a real (unconnected)
/// Sharpcaster ChromecastClient — no live Chromecast device involved.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class ChromeCastServiceReflectionUtilityTests
{
    private sealed class NoOpNetworkDiscovery : INetworkDiscovery
    {
        public string InternalIp { get; set; } = "192.168.1.1";
        public string RegistrationInternalIp => InternalIp;
        public string ExternalIp { get; set; } = "1.2.3.4";
        public string? InternalIpV6 => null;
        public string? ExternalIpV6 { get; set; }
        public string InternalDomain => string.Empty;
        public string InternalAddress => string.Empty;
        public string ExternalDomain => string.Empty;
        public string ExternalAddress => string.Empty;
        public string? ExternalAddressV6 => null;
        public bool Ipv6Enabled => false;

        public Task DiscoverExternalIpAsync() => Task.CompletedTask;

        public Task ForceRediscoveryAsync() => Task.CompletedTask;

        public Task<bool> IsPortOpenAsync() => Task.FromResult(result: false);
    }

    private static ChromeCastService BuildService() =>
        new(logger: NullLogger<ChromeCastService>.Instance, networkDiscovery: new NoOpNetworkDiscovery());

    [Fact]
    public void NeutralizeTimer_StopsTheTimer_AndDisablesAutoReset()
    {
        ChromeCastService service = BuildService();
        Timer timer = new(interval: 1000) { AutoReset = true };
        timer.Start();

        service.NeutralizeTimer(timer: timer);

        Assert.False(condition: timer.Enabled);
        Assert.False(condition: timer.AutoReset);
    }

    [Fact]
    public void NeutralizeTimer_ReplacesElapsedHandler_SoOriginalNeverFires()
    {
        ChromeCastService service = BuildService();
        Timer timer = new(interval: 1000);
        bool originalHandlerFired = false;
        timer.Elapsed += (_, _) => originalHandlerFired = true;

        service.NeutralizeTimer(timer: timer);

        // Force-invoke whatever handler is now wired to the timer's private
        // _onIntervalElapsed field — after neutralizing, that must not be the
        // original handler we attached above.
        FieldInfo? field = typeof(Timer).GetField(
            name: "_onIntervalElapsed",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.NotNull(@object: field);
        object? handler = field!.GetValue(obj: timer);
        Assert.NotNull(@object: handler);

        ((ElapsedEventHandler)handler!).Invoke(sender: timer, e: null!);

        Assert.False(condition: originalHandlerFired);
    }

    [Fact]
    public void NeutralizeTimer_DoesNotDisposeTheTimer()
    {
        ChromeCastService service = BuildService();
        Timer timer = new(interval: 1000);

        service.NeutralizeTimer(timer: timer);

        // A disposed Timer throws ObjectDisposedException from set_Enabled —
        // this must not throw, proving the timer was left alive.
        Exception? ex = Record.Exception(testCode: () => timer.Enabled = true);
        Assert.Null(@object: ex);
        timer.Dispose();
    }

    [Fact]
    public void NeutralizeTimer_CalledTwice_DoesNotThrow()
    {
        ChromeCastService service = BuildService();
        Timer timer = new(interval: 1000);

        Exception? ex = Record.Exception(testCode: () =>
        {
            service.NeutralizeTimer(timer: timer);
            service.NeutralizeTimer(timer: timer);
        });

        Assert.Null(@object: ex);
        timer.Dispose();
    }

    private sealed class OwnerWithPrivateTimer
    {
        private readonly Timer _timer = new(interval: 1000);

        public Timer Timer => _timer;
    }

    [Fact]
    public void NeutralizeTimersIn_FindsAndStopsPrivateTimerField()
    {
        ChromeCastService service = BuildService();
        OwnerWithPrivateTimer owner = new();
        owner.Timer.Start();

        service.NeutralizeTimersIn(owner: owner);

        Assert.False(condition: owner.Timer.Enabled);
        owner.Timer.Dispose();
    }

    private sealed class OwnerWithNoTimerFields
    {
        private readonly string _label = "no timers here";

        public string Label => _label;
    }

    [Fact]
    public void NeutralizeTimersIn_ObjectWithoutTimerFields_DoesNotThrow()
    {
        ChromeCastService service = BuildService();

        Exception? ex = Record.Exception(testCode: () =>
            service.NeutralizeTimersIn(owner: new OwnerWithNoTimerFields())
        );

        Assert.Null(@object: ex);
    }

    [Fact]
    public void BuildClient_ReturnsNonNullClient_AndDoesNotThrow()
    {
        ChromeCastService service = BuildService();

        ChromecastClient? client = null;
        Exception? ex = Record.Exception(testCode: () => client = service.BuildClient(receiverName: "Living Room TV"));

        Assert.Null(@object: ex);
        Assert.NotNull(@object: client);
    }

    [Fact]
    public void DisableSharpcasterHeartbeat_AgainstBareUnconnectedClient_DoesNotThrow()
    {
        ChromeCastService service = BuildService();
        ChromecastClient client = new();

        Exception? ex = Record.Exception(testCode: () => service.DisableSharpcasterHeartbeat(client: client));

        Assert.Null(@object: ex);
    }

    [Fact]
    public void DisableSharpcasterHeartbeat_CalledTwice_DoesNotThrow()
    {
        ChromeCastService service = BuildService();
        ChromecastClient client = new();

        Exception? ex = Record.Exception(testCode: () =>
        {
            service.DisableSharpcasterHeartbeat(client: client);
            service.DisableSharpcasterHeartbeat(client: client);
        });

        Assert.Null(@object: ex);
    }

    // -- BuildLaunchJson: all four LAUNCH-payload permutations.

    [Fact]
    public void BuildLaunchJson_NoCustomData_AndroidReceiver_ContainsAndroidLaunchOptions()
    {
        ChromeCastService service = BuildService();

        string json = service.BuildLaunchJson(requestId: 1, customData: null, useAndroidReceiver: true);

        Assert.Contains(expectedSubstring: "\"appId\":\"925B4C3C\"", actualString: json);
        Assert.Contains(expectedSubstring: "\"requestId\":1", actualString: json);
        Assert.Contains(expectedSubstring: "ANDROID_TV", actualString: json);
        Assert.Contains(expectedSubstring: "androidReceiverCompatible", actualString: json);
        Assert.DoesNotContain(expectedSubstring: "customData", actualString: json);
    }

    [Fact]
    public void BuildLaunchJson_NoCustomData_WebReceiver_ContainsWebAppType()
    {
        ChromeCastService service = BuildService();

        string json = service.BuildLaunchJson(requestId: 2, customData: null, useAndroidReceiver: false);

        Assert.Contains(expectedSubstring: "\"appId\":\"925B4C3C\"", actualString: json);
        Assert.Contains(expectedSubstring: "\"requestId\":2", actualString: json);
        Assert.Contains(expectedSubstring: "\"WEB\"", actualString: json);
        Assert.DoesNotContain(expectedSubstring: "androidReceiverCompatible", actualString: json);
        Assert.DoesNotContain(expectedSubstring: "customData", actualString: json);
    }

    [Fact]
    public void BuildLaunchJson_WithCustomData_AndroidReceiver_IncludesCustomData()
    {
        ChromeCastService service = BuildService();

        string json = service.BuildLaunchJson(
            requestId: 3,
            customData: new { accessToken = "abc" },
            useAndroidReceiver: true
        );

        Assert.Contains(expectedSubstring: "androidReceiverCompatible", actualString: json);
        Assert.Contains(expectedSubstring: "customData", actualString: json);
        Assert.Contains(expectedSubstring: "abc", actualString: json);
    }

    [Fact]
    public void BuildLaunchJson_WithCustomData_WebReceiver_IncludesCustomData()
    {
        ChromeCastService service = BuildService();

        string json = service.BuildLaunchJson(
            requestId: 4,
            customData: new { accessToken = "xyz" },
            useAndroidReceiver: false
        );

        Assert.Contains(expectedSubstring: "\"WEB\"", actualString: json);
        Assert.Contains(expectedSubstring: "customData", actualString: json);
        Assert.Contains(expectedSubstring: "xyz", actualString: json);
        Assert.DoesNotContain(expectedSubstring: "androidReceiverCompatible", actualString: json);
    }

    [Fact]
    public void BuildLaunchJson_RequestIdIsEmbeddedVerbatim()
    {
        ChromeCastService service = BuildService();

        string json = service.BuildLaunchJson(requestId: 999, customData: null, useAndroidReceiver: true);

        Assert.Contains(expectedSubstring: "\"requestId\":999", actualString: json);
    }
}
