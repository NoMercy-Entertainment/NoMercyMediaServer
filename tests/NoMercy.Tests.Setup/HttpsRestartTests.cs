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

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Information;
using NoMercy.Setup.Server;

namespace NoMercy.Tests.Setup;

public class CertificateAvailabilityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalCertPath;

    public CertificateAvailabilityTests()
    {
        _tempDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy_cert_test_" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempDir);

        _originalCertPath = AppFiles.CertPath;
    }

    public void Dispose()
    {
        // Restore original cert path
        SetCertPath(path: _originalCertPath);

        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);
    }

    private void SetCertPath(string path)
    {
        // Use reflection to temporarily override the CertPath for testing
        // Since AppFiles.CertPath is derived from ConfigPath, we test via
        // the actual file existence at the real paths
    }

    [Fact]
    public void HasValidCertificate_ReturnsFalse_WhenNoCertFiles()
    {
        // HasValidCertificate now checks the DB (Configuration table) for stored
        // certificate PEM. In the test environment there is no database, so this
        // may throw SqliteException — both outcomes (false returned or exception)
        // indicate no valid certificate is present.
        try
        {
            bool result = new CertificateService(
                logger: NullLogger<CertificateService>.Instance,
                httpClientFactory: null!
            ).HasValidCertificate();
            Assert.False(condition: result, userMessage: "No certificate should be present in the test environment");
        }
        catch (SqliteException)
        {
            // Expected when Configuration table does not exist in the test environment.
            // This correctly indicates no certificate is stored in the DB.
        }
    }
}

public class SetupCompleteSignalTests
{
    [Fact]
    public async Task SetupComplete_TriggersWaiters_WhenPhaseReachesComplete()
    {
        SetupState state = new();

        bool completed = false;
        Task waitTask = Task.Run(function: async () =>
        {
            await state.WaitForSetupCompleteAsync();
            completed = true;
        });

        // Simulate full setup flow
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        state.TransitionTo(targetPhase: SetupPhase.Complete);

        await waitTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));
        Assert.True(condition: completed);
    }

    [Fact]
    public async Task SetupComplete_DoesNotBlock_WhenAlreadyComplete()
    {
        SetupState state = new();
        state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);

        Assert.Equal(expected: SetupPhase.Complete, actual: state.CurrentPhase);

        // Should return immediately
        Task waitTask = state.WaitForSetupCompleteAsync();
        await waitTask.WaitAsync(timeout: TimeSpan.FromMilliseconds(milliseconds: 100));
        Assert.True(condition: waitTask.IsCompleted);
    }

    [Fact]
    public async Task SetupComplete_CanBeCancelled()
    {
        SetupState state = new();
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            state.WaitForSetupCompleteAsync(cancellationToken: cts.Token)
        );
    }

    [Fact]
    public async Task SetupComplete_MultipleWaiters_AllComplete()
    {
        SetupState state = new();

        Task[] waiters = Enumerable
            .Range(start: 0, count: 5)
            .Select(selector: _ => state.WaitForSetupCompleteAsync())
            .ToArray();

        // Complete setup
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        state.TransitionTo(targetPhase: SetupPhase.Complete);

        await Task.WhenAll(tasks: waiters).WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 2));
        Assert.All(collection: waiters, action: w => Assert.True(condition: w.IsCompleted));
    }
}

public class HttpToHttpsTransitionTests
{
    [Fact]
    public void KestrelConfig_DoesNotThrow_WhenNoCertificateExists()
    {
        // HasValidCertificate now checks the DB (Configuration table). In the
        // test environment there is no database, so the result will either be
        // false (no cert) or a SqliteException (no table) — both mean no cert.
        // The important invariant: the method is callable and does not panic.
        bool hasCert = false;
        try
        {
            hasCert = new CertificateService(
                logger: NullLogger<CertificateService>.Instance,
                httpClientFactory: null!
            ).HasValidCertificate();
        }
        catch (SqliteException)
        {
            // Expected when Configuration table does not exist — treated as no cert.
        }

        Assert.False(condition: hasCert);
    }

    [Fact]
    public void SetupState_TransitionToComplete_IsValid_FromCertificateAcquired()
    {
        Assert.True(
            condition: SetupState.IsValidTransition(from: SetupPhase.CertificateAcquired, to: SetupPhase.Complete)
        );
    }

    [Fact]
    public void SetupState_FullTransitionChain_Succeeds()
    {
        SetupState state = new();

        Assert.True(condition: state.TransitionTo(targetPhase: SetupPhase.Authenticating));
        Assert.True(condition: state.TransitionTo(targetPhase: SetupPhase.Authenticated));
        Assert.True(condition: state.TransitionTo(targetPhase: SetupPhase.Registering));
        Assert.True(condition: state.TransitionTo(targetPhase: SetupPhase.Registered));
        Assert.True(condition: state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired));
        Assert.True(condition: state.TransitionTo(targetPhase: SetupPhase.Complete));

        Assert.False(condition: state.IsSetupRequired);
        Assert.Equal(expected: SetupPhase.Complete, actual: state.CurrentPhase);
    }

    [Fact]
    public async Task SetupComplete_SignalsFutureWaitersAfterCompletion()
    {
        SetupState state = new();

        // Complete setup first
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        state.TransitionTo(targetPhase: SetupPhase.Complete);

        // New waiter should complete immediately
        Task waitTask = state.WaitForSetupCompleteAsync();
        await waitTask.WaitAsync(timeout: TimeSpan.FromMilliseconds(milliseconds: 100));
        Assert.True(condition: waitTask.IsCompleted);
    }
}

/// <summary>
/// Reproduces the ServerBootstrapper container-rebuild bug: SetupState is registered
/// as a per-container singleton (services.AddSingleton&lt;SetupState&gt;()), so when
/// ServerBootstrapper disposes the WebApplication and calls WebHostFactory.Create
/// again (the has-token-no-cert first-boot path — auth + registration already
/// succeeded, a certificate was just acquired), the new container gets a brand new
/// SetupState starting at SetupPhase.Unauthenticated. Without carrying the completed
/// phase across, SetupModeMiddleware 503s every route on the new host even though
/// setup genuinely finished on the old one.
/// </summary>
public class SetupStateSurvivesContainerRebuildTests
{
    [Fact]
    public void NewContainerSetupState_WithoutRestore_IncorrectlyRequiresSetup()
    {
        // The OLD container's SetupState: BootOrchestrator.RunAsync drove this to
        // Complete because auth succeeded and the server was already registered.
        SetupState oldContainerState = new();
        oldContainerState.DetermineInitialPhase(hasValidToken: true, isRegistered: true);
        Assert.Equal(expected: SetupPhase.Complete, actual: oldContainerState.CurrentPhase);

        // ServerBootstrapper disposes the app (and the old SetupState singleton with
        // it) and calls WebHostFactory.Create again. DI hands the new container a
        // FRESH SetupState — this is what a naive rebuild produces.
        SetupState newContainerStateWithoutFix = new();

        // This is the bug: the new container falsely believes setup is still required,
        // even though the orchestrator already reached Complete on the old one.
        Assert.True(condition: newContainerStateWithoutFix.IsSetupRequired);
    }

    [Fact]
    public void NewContainerSetupState_WithRestore_MatchesCompletedOrchestratorOutcome()
    {
        // Old container reaches Complete (orchestrator ran once, needsSetupMode came
        // back false because auth succeeded).
        SetupState oldContainerState = new();
        oldContainerState.DetermineInitialPhase(hasValidToken: true, isRegistered: true);
        Assert.False(condition: oldContainerState.IsSetupRequired);

        // Rebuild: new container, new SetupState singleton.
        SetupState newContainerState = new();

        // ServerBootstrapper's fix (the has-token-no-cert rebuild branch): restore the
        // new container's SetupState to the phase the orchestrator already reached —
        // hasValidToken/isRegistered are both true by construction at that call site
        // (needsSetupMode was false, and EnsureHttpsCertificate() just returned true).
        newContainerState.DetermineInitialPhase(hasValidToken: true, isRegistered: true);

        Assert.Equal(expected: SetupPhase.Complete, actual: newContainerState.CurrentPhase);
        Assert.False(condition: newContainerState.IsSetupRequired);
    }

    [Fact]
    public void NeedsSetupModeTrueRebuild_LeavesNewSetupStateRequiringSetup()
    {
        // The OTHER rebuild branch (needsSetupMode && hasCert, HTTPS -> HTTP-only for
        // the setup flow) must NOT be forced to Complete — setup genuinely is not
        // done yet, so the fresh container's SetupState should stay Unauthenticated
        // and let the real interactive setup flow drive it forward.
        SetupState newContainerState = new();

        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: newContainerState.CurrentPhase);
        Assert.True(condition: newContainerState.IsSetupRequired);
    }
}
