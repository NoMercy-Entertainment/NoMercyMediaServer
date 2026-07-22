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

namespace NoMercy.Tests.Setup.Server;

[Trait(name: "Category", value: "Data")]
public class SetupStateSignalingTests
{
    [Fact]
    public async Task WaitForChangeAsync_completes_when_transition_occurs()
    {
        SetupState state = new();
        Task waitTask = state.WaitForChangeAsync();

        await Task.Delay(millisecondsDelay: 10);
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        await waitTask;
    }

    [Fact]
    public async Task WaitForChangeAsync_cancellation_token_honored()
    {
        SetupState state = new();
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            state.WaitForChangeAsync(cancellationToken: cts.Token)
        );
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_completes_immediately_when_already_complete()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        state.TransitionTo(targetPhase: SetupPhase.Complete);

        Task completeTask = state.WaitForSetupCompleteAsync();

        await completeTask;
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_waits_until_complete()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        Task waitTask = state.WaitForSetupCompleteAsync();
        Assert.False(condition: waitTask.IsCompleted);

        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        await Task.Delay(millisecondsDelay: 10);
        Assert.False(condition: waitTask.IsCompleted);

        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        state.TransitionTo(targetPhase: SetupPhase.Complete);

        await waitTask;
    }

    [Fact]
    public async Task WaitForPhaseAsync_completes_when_phase_reached()
    {
        SetupState state = new();
        Task waitTask = state.WaitForPhaseAsync(targetPhase: SetupPhase.Authenticated);

        Assert.False(condition: waitTask.IsCompleted);

        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        await Task.Delay(millisecondsDelay: 10);
        Assert.False(condition: waitTask.IsCompleted);

        state.TransitionTo(targetPhase: SetupPhase.Authenticated);

        await waitTask;
    }

    [Fact]
    public async Task WaitForPhaseAsync_completes_immediately_when_phase_already_reached()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);

        Task waitTask = state.WaitForPhaseAsync(targetPhase: SetupPhase.Authenticated);

        await waitTask;
    }

    [Fact]
    public async Task WaitForPhaseAsync_completes_when_phase_surpassed()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        Task waitTask = state.WaitForPhaseAsync(targetPhase: SetupPhase.Authenticated);

        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);

        await waitTask;
    }

    [Fact]
    public async Task WaitForPhaseAsync_cancellation_token_honored()
    {
        SetupState state = new();
        using CancellationTokenSource cts = new(delay: TimeSpan.FromMilliseconds(milliseconds: 50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(testCode: () =>
            state.WaitForPhaseAsync(targetPhase: SetupPhase.Complete, cancellationToken: cts.Token)
        );
    }

    [Fact]
    public async Task SetPhaseDetail_signals_change()
    {
        SetupState state = new();
        Task waitTask = state.WaitForChangeAsync();

        await Task.Delay(millisecondsDelay: 10);
        state.SetPhaseDetail(detail: "New detail");

        await waitTask;
    }

    [Fact]
    public async Task SetError_signals_change()
    {
        SetupState state = new();
        Task waitTask = state.WaitForChangeAsync();

        await Task.Delay(millisecondsDelay: 10);
        state.SetError(message: "Error message");

        await waitTask;
    }

    [Fact]
    public async Task ClearError_signals_change()
    {
        SetupState state = new();
        state.SetError(message: "Error message");

        Task waitTask = state.WaitForChangeAsync();

        await Task.Delay(millisecondsDelay: 10);
        state.ClearError();

        await waitTask;
    }

    [Fact]
    public async Task Reset_signals_change()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        Task waitTask = state.WaitForChangeAsync();

        await Task.Delay(millisecondsDelay: 10);
        state.Reset();

        await waitTask;
    }

    [Fact]
    public async Task Multiple_waiters_all_notified_on_change()
    {
        SetupState state = new();

        Task wait1 = state.WaitForChangeAsync();
        Task wait2 = state.WaitForChangeAsync();
        Task wait3 = state.WaitForChangeAsync();

        await Task.Delay(millisecondsDelay: 10);
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        await Task.WhenAll(tasks: [wait1, wait2, wait3]);
    }

    [Fact]
    public async Task Subsequent_wait_after_change_gets_next_signal()
    {
        SetupState state = new();

        Task wait1 = state.WaitForChangeAsync();
        await Task.Delay(millisecondsDelay: 10);
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        await wait1;

        Task wait2 = state.WaitForChangeAsync();
        Assert.False(condition: wait2.IsCompleted);

        await Task.Delay(millisecondsDelay: 10);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);

        await wait2;
    }
}
