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
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NoMercy.Networking.Certificate;
using NoMercy.Setup.Boot;

namespace NoMercy.Tests.Setup.Boot;

/// <summary>
/// Requirement: <see cref="Start.InitRemaining"/> must derive <c>DeferredTasks.Registered</c>
/// from a real fact (<see cref="ICertificateService.HasValidCertificate"/> — the same one
/// <c>BootOrchestrator</c> itself uses to decide <c>isRegistered</c>), not from a
/// <c>StartupTask</c> named "Register" that <see cref="Start.BuildStartupTasks"/> never
/// produces. The old shape read <c>runner.CompletedTasks.Contains("Register")</c>, which was
/// permanently false — <c>DegradedModeRecovery.StartRecoveryLoop</c> requires
/// <c>Registered: true</c> for <c>AllCompleted</c>, so an already-registered server stuck in
/// degraded mode (e.g. a deferred Binaries task) re-entered registration — and its 60s
/// cooldown — forever, never converging.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StartInitRemainingRegisteredFactTests : IDisposable
{
    public StartInitRemainingRegisteredFactTests()
    {
        ResetStaticState();
    }

    public void Dispose()
    {
        ResetStaticState();
    }

    private static void ResetStaticState()
    {
        SetStaticField(typeof(Start), "_allTasks", new List<StartupTask>());
        SetStaticField(typeof(Start), "_phase1Completed", new HashSet<string>());
        SetStaticField(typeof(Start), "_essentialInitialized", false);
        Start.Certificate = null;
        Start.IsDegradedMode = false;
    }

    private static void SetStaticField(Type type, string name, object? value)
    {
        FieldInfo field =
            type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{type.Name} has no static field '{name}'");
        field.SetValue(null, value);
    }

    /// <summary>A single Phase 2, CanDefer:true task whose Action always throws — forces
    /// StartupTaskRunner to defer it, so InitRemaining's DeferredTasks-building branch runs
    /// without needing a real network/hardware dependency to fail.</summary>
    private static void InstallSingleDeferringTask()
    {
        SetStaticField(
            typeof(Start),
            "_allTasks",
            new List<StartupTask>
            {
                new(
                    "AlwaysDefers",
                    () => throw new InvalidOperationException("simulated transient failure"),
                    CanDefer: true,
                    Phase: 2
                ),
            }
        );
    }

    private sealed class StubCertificateService(bool hasValidCertificate) : ICertificateService
    {
        public void LoadFromDb() { }

        public bool HasValidCertificate() => hasValidCertificate;

        public bool EnsureHttpsCertificate() => hasValidCertificate;

        public void KestrelConfig(KestrelServerOptions options) { }

        public void ConfigureHttpsListener(ListenOptions listenOptions) { }

        public Task RenewSslCertificate(string? accessToken, int maxRetries = 30) =>
            Task.CompletedTask;
    }

    private sealed class CapturingDegradedModeRecovery : IDegradedModeRecovery
    {
        private readonly TaskCompletionSource<DeferredTasks> _captured = new();

        public Task<DeferredTasks> Captured => _captured.Task;

        public Task StartRecoveryLoop(DeferredTasks tasks)
        {
            _captured.TrySetResult(tasks);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task InitRemaining_CertificateAlreadyValid_RegisteredStartsTrue()
    {
        Start.Certificate = new StubCertificateService(hasValidCertificate: true);
        InstallSingleDeferringTask();
        CapturingDegradedModeRecovery recovery = new();

        await Start.InitRemaining(recovery, accessToken: "token");
        DeferredTasks captured = await recovery.Captured.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(
            captured.Registered,
            "an already-registered server (valid cert) must not re-enter registration on every deferred-task recovery tick"
        );
    }

    [Fact]
    public async Task InitRemaining_NoCertificateYet_RegisteredStartsFalse()
    {
        Start.Certificate = new StubCertificateService(hasValidCertificate: false);
        InstallSingleDeferringTask();
        CapturingDegradedModeRecovery recovery = new();

        await Start.InitRemaining(recovery, accessToken: "token");
        DeferredTasks captured = await recovery.Captured.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(captured.Registered);
    }

    [Fact]
    public void BuildStartupTasks_ContainsNoTaskNamedRegister()
    {
        // Pins the root cause directly: registration runs in BootOrchestrator, not as a
        // StartupTask here — CompletedTasks.Contains("Register") could only ever be false.
        List<StartupTask> tasks = Start.BuildStartupTasks();

        Assert.DoesNotContain(tasks, t => t.Name == "Register");
    }
}
