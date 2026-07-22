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

using NoMercy.Setup.Server;

namespace NoMercy.Tests.Setup;

public class SetupStateTests
{
    // --- Initial State ---

    [Fact]
    public void NewState_StartsAsUnauthenticated()
    {
        SetupState state = new();
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: state.CurrentPhase);
    }

    [Fact]
    public void NewState_IsSetupRequired()
    {
        SetupState state = new();
        Assert.True(condition: state.IsSetupRequired);
    }

    [Fact]
    public void NewState_IsNotAuthenticated()
    {
        SetupState state = new();
        Assert.False(condition: state.IsAuthenticated);
    }

    [Fact]
    public void NewState_HasNoError()
    {
        SetupState state = new();
        Assert.Null(@object: state.ErrorMessage);
    }

    // --- Forward Transitions ---

    [Fact]
    public void TransitionTo_Authenticating_FromUnauthenticated_Succeeds()
    {
        SetupState state = new();
        bool result = state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Authenticating, actual: state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Authenticated_FromAuthenticating_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        bool result = state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Authenticated, actual: state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Registering_FromAuthenticated_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        bool result = state.TransitionTo(targetPhase: SetupPhase.Registering);
        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Registering, actual: state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Registered_FromRegistering_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        bool result = state.TransitionTo(targetPhase: SetupPhase.Registered);
        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Registered, actual: state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_CertificateAcquired_FromRegistered_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        bool result = state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.CertificateAcquired, actual: state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Complete_FromCertificateAcquired_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        bool result = state.TransitionTo(targetPhase: SetupPhase.Complete);
        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Complete, actual: state.CurrentPhase);
    }

    [Fact]
    public void Complete_IsNotSetupRequired()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        state.TransitionTo(targetPhase: SetupPhase.Complete);
        Assert.False(condition: state.IsSetupRequired);
    }

    [Fact]
    public void Authenticated_IsAuthenticated()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        Assert.True(condition: state.IsAuthenticated);
    }

    // --- Invalid Transitions ---

    [Fact]
    public void TransitionTo_Complete_FromUnauthenticated_Fails()
    {
        SetupState state = new();
        bool result = state.TransitionTo(targetPhase: SetupPhase.Complete);
        Assert.False(condition: result);
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Registered_FromUnauthenticated_Fails()
    {
        SetupState state = new();
        bool result = state.TransitionTo(targetPhase: SetupPhase.Registered);
        Assert.False(condition: result);
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Authenticated_FromUnauthenticated_Fails()
    {
        SetupState state = new();
        bool result = state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        Assert.False(condition: result);
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: state.CurrentPhase);
    }

    // --- Error Recovery Transitions ---

    [Fact]
    public void TransitionTo_Unauthenticated_FromAuthenticating_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        bool result = state.TransitionTo(targetPhase: SetupPhase.Unauthenticated);
        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Authenticated_FromRegistering_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        bool result = state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Authenticated, actual: state.CurrentPhase);
    }

    // --- Error Handling ---

    [Fact]
    public void SetError_StoresMessage()
    {
        SetupState state = new();
        state.SetError(message: "Network timeout");
        Assert.Equal(expected: "Network timeout", actual: state.ErrorMessage);
    }

    [Fact]
    public void TransitionTo_ClearsError()
    {
        SetupState state = new();
        state.SetError(message: "Some error");
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        Assert.Null(@object: state.ErrorMessage);
    }

    // --- Reset ---

    [Fact]
    public void Reset_ReturnsToUnauthenticated()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.SetError(message: "some error");
        state.Reset();
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: state.CurrentPhase);
        Assert.Null(@object: state.ErrorMessage);
    }

    // --- IsValidTransition ---

    [Theory]
    [InlineData(data: [SetupPhase.Unauthenticated, SetupPhase.Authenticating, true])]
    [InlineData(data: [SetupPhase.Authenticating, SetupPhase.Authenticated, true])]
    [InlineData(data: [SetupPhase.Authenticated, SetupPhase.Registering, true])]
    [InlineData(data: [SetupPhase.Registering, SetupPhase.Registered, true])]
    [InlineData(data: [SetupPhase.Registered, SetupPhase.CertificateAcquired, true])]
    [InlineData(data: [SetupPhase.CertificateAcquired, SetupPhase.Complete, true])]
    [InlineData(data: [SetupPhase.Authenticating, SetupPhase.Unauthenticated, true])]
    [InlineData(data: [SetupPhase.Registering, SetupPhase.Authenticated, true])]
    [InlineData(data: [SetupPhase.Unauthenticated, SetupPhase.Complete, false])]
    [InlineData(data: [SetupPhase.Unauthenticated, SetupPhase.Registered, false])]
    [InlineData(data: [SetupPhase.Complete, SetupPhase.Unauthenticated, false])]
    // Degraded-complete: BootOrchestrator.RunRegistrationAsync reaches Complete even
    // when the certificate isn't ready yet (Registered, no cert) or registration
    // itself failed (still at Registering when its own catch block runs) — see the
    // BootOrchestratorAdditionalTests that exercise these two call sites directly.
    // Regression coverage for a real bug: before these were added, both call sites'
    // TransitionTo(Complete) was silently rejected and left setup permanently stuck.
    [InlineData(data: [SetupPhase.Registered, SetupPhase.Complete, true])]
    [InlineData(data: [SetupPhase.Registering, SetupPhase.Complete, true])]
    public void IsValidTransition_ReturnsExpected(SetupPhase from, SetupPhase to, bool expected)
    {
        Assert.Equal(expected: expected, actual: SetupState.IsValidTransition(from: from, to: to));
    }

    // --- WaitForChangeAsync ---

    [Fact]
    public async Task WaitForChangeAsync_CompletesOnTransition()
    {
        SetupState state = new();
        Task waitTask = state.WaitForChangeAsync();

        Assert.False(condition: waitTask.IsCompleted);

        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        await waitTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1));
        Assert.True(condition: waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_CompletesOnSetError()
    {
        SetupState state = new();
        Task waitTask = state.WaitForChangeAsync();

        Assert.False(condition: waitTask.IsCompleted);

        state.SetError(message: "test error");

        await waitTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1));
        Assert.True(condition: waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_CompletesOnReset()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        Task waitTask = state.WaitForChangeAsync();
        Assert.False(condition: waitTask.IsCompleted);

        state.Reset();

        await waitTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1));
        Assert.True(condition: waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_SupportsMultipleWaiters()
    {
        SetupState state = new();
        Task wait1 = state.WaitForChangeAsync();
        Task wait2 = state.WaitForChangeAsync();

        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        await Task.WhenAll(tasks: [wait1.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1)), wait2.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1))]
        );

        Assert.True(condition: wait1.IsCompleted);
        Assert.True(condition: wait2.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_CanBeCalledAgainAfterChange()
    {
        SetupState state = new();

        Task wait1 = state.WaitForChangeAsync();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        await wait1.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1));

        Task wait2 = state.WaitForChangeAsync();
        Assert.False(condition: wait2.IsCompleted);

        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        await wait2.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1));
        Assert.True(condition: wait2.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_RespectsCancellation()
    {
        SetupState state = new();
        using CancellationTokenSource cts = new();

        Task waitTask = state.WaitForChangeAsync(cancellationToken: cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            waitTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1))
        );
    }

    // --- WaitForSetupCompleteAsync ---

    [Fact]
    public async Task WaitForSetupCompleteAsync_CompletesWhenTransitionedToComplete()
    {
        SetupState state = new();
        Task waitTask = state.WaitForSetupCompleteAsync();

        Assert.False(condition: waitTask.IsCompleted);

        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);

        Assert.False(condition: waitTask.IsCompleted);

        state.TransitionTo(targetPhase: SetupPhase.Complete);

        await waitTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1));
        Assert.True(condition: waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_CompletesImmediatelyWhenAlreadyComplete()
    {
        SetupState state = new();
        state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);

        Task waitTask = state.WaitForSetupCompleteAsync();

        await waitTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1));
        Assert.True(condition: waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_RespectsCancellation()
    {
        SetupState state = new();
        using CancellationTokenSource cts = new();

        Task waitTask = state.WaitForSetupCompleteAsync(cancellationToken: cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            waitTask.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1))
        );
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_SupportsMultipleWaiters()
    {
        SetupState state = new();
        Task wait1 = state.WaitForSetupCompleteAsync();
        Task wait2 = state.WaitForSetupCompleteAsync();

        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        state.TransitionTo(targetPhase: SetupPhase.Complete);

        await Task.WhenAll(tasks: [wait1.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1)), wait2.WaitAsync(timeout: TimeSpan.FromSeconds(seconds: 1))]
        );

        Assert.True(condition: wait1.IsCompleted);
        Assert.True(condition: wait2.IsCompleted);
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_DoesNotCompleteOnIntermediatePhases()
    {
        SetupState state = new();
        Task waitTask = state.WaitForSetupCompleteAsync();

        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);

        // Give it a moment to ensure it doesn't complete prematurely
        await Task.Delay(millisecondsDelay: 50);
        Assert.False(condition: waitTask.IsCompleted);
    }

    // --- DetermineInitialPhase ---

    [Fact]
    public void DetermineInitialPhase_ValidTokenRegistered_SetsComplete()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);
        Assert.Equal(expected: SetupPhase.Complete, actual: phase);
        Assert.Equal(expected: SetupPhase.Complete, actual: state.CurrentPhase);
    }

    [Fact]
    public void DetermineInitialPhase_ValidTokenNotRegistered_SetsAuthenticated()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(hasValidToken: true, isRegistered: false);
        Assert.Equal(expected: SetupPhase.Authenticated, actual: phase);
    }

    [Fact]
    public void DetermineInitialPhase_NoToken_StaysUnauthenticated()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(hasValidToken: false);
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: phase);
    }
}
