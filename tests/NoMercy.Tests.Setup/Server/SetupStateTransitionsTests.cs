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

[Trait("Category", "Data")]
public class SetupStateTransitionsTests
{
    [Fact]
    public void InvalidTransition_Authenticating_to_Registered_fails()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);

        bool result = state.TransitionTo(SetupPhase.Registered);

        Assert.False(result);
        Assert.Equal(SetupPhase.Authenticating, state.CurrentPhase);
    }

    [Fact]
    public void InvalidTransition_Authenticated_to_Registering_skipping_phases_fails()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);

        bool result = state.TransitionTo(SetupPhase.CertificateAcquired);

        Assert.False(result);
        Assert.Equal(SetupPhase.Authenticated, state.CurrentPhase);
    }

    [Fact]
    public void InvalidTransition_Registered_to_Unauthenticated_backward_to_start_fails()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);

        bool result = state.TransitionTo(SetupPhase.Unauthenticated);

        Assert.False(result);
        Assert.Equal(SetupPhase.Registered, state.CurrentPhase);
    }

    [Fact]
    public void InvalidTransition_Complete_to_Registered_backward_fails()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);
        state.TransitionTo(SetupPhase.CertificateAcquired);
        state.TransitionTo(SetupPhase.Complete);

        bool result = state.TransitionTo(SetupPhase.Registered);

        Assert.False(result);
        Assert.Equal(SetupPhase.Complete, state.CurrentPhase);
    }

    [Fact]
    public void ErrorRecoveryTransition_Authenticating_to_Unauthenticated_succeeds()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);

        bool result = state.TransitionTo(SetupPhase.Unauthenticated);

        Assert.True(result);
        Assert.Equal(SetupPhase.Unauthenticated, state.CurrentPhase);
    }

    [Fact]
    public void ErrorRecoveryTransition_Registering_to_Authenticated_succeeds()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);

        bool result = state.TransitionTo(SetupPhase.Authenticated);

        Assert.True(result);
        Assert.Equal(SetupPhase.Authenticated, state.CurrentPhase);
    }

    [Fact]
    public void RetryTransition_Authenticated_stays_Authenticated()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);

        bool result = state.TransitionTo(SetupPhase.Authenticated);

        Assert.True(result);
        Assert.Equal(SetupPhase.Authenticated, state.CurrentPhase);
    }

    [Fact]
    public void RetryTransition_Registered_stays_Registered()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);

        bool result = state.TransitionTo(SetupPhase.Registered);

        Assert.True(result);
        Assert.Equal(SetupPhase.Registered, state.CurrentPhase);
    }

    [Fact]
    public void SetError_preserves_current_phase()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);

        state.SetError("Authentication failed");

        Assert.Equal(SetupPhase.Authenticating, state.CurrentPhase);
        Assert.Equal("Authentication failed", state.ErrorMessage);
    }

    [Fact]
    public void ClearError_removes_error_message()
    {
        SetupState state = new();
        state.SetError("Some error");

        state.ClearError();

        Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public void ErrorOnTransition_clears_previous_error()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.SetError("Previous error");

        state.TransitionTo(SetupPhase.Authenticated);

        Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public void SetPhaseDetail_updates_detail()
    {
        SetupState state = new();

        state.SetPhaseDetail("Custom detail message");

        Assert.Equal("Custom detail message", state.PhaseDetail);
    }

    [Fact]
    public void SetServerUrl_updates_url()
    {
        SetupState state = new();

        state.SetServerUrl("https://nomercy.local:8080");

        Assert.Equal("https://nomercy.local:8080", state.ServerUrl);
    }

    [Fact]
    public void Reset_returns_to_Unauthenticated()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.SetError("Some error");

        state.Reset();

        Assert.Equal(SetupPhase.Unauthenticated, state.CurrentPhase);
        Assert.Null(state.ErrorMessage);
    }

    [Fact]
    public void DetermineInitialPhase_with_valid_token_and_registered_returns_complete()
    {
        SetupState state = new();

        SetupPhase result = state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);

        Assert.Equal(SetupPhase.Complete, result);
        Assert.Equal(SetupPhase.Complete, state.CurrentPhase);
        Assert.False(state.IsSetupRequired);
    }

    [Fact]
    public void DetermineInitialPhase_with_valid_token_not_registered_returns_authenticated()
    {
        SetupState state = new();

        SetupPhase result = state.DetermineInitialPhase(hasValidToken: true, isRegistered: false);

        Assert.Equal(SetupPhase.Authenticated, result);
        Assert.Equal(SetupPhase.Authenticated, state.CurrentPhase);
        Assert.True(state.IsSetupRequired);
        Assert.True(state.IsAuthenticated);
    }

    [Fact]
    public void DetermineInitialPhase_without_valid_token_returns_unauthenticated()
    {
        SetupState state = new();

        SetupPhase result = state.DetermineInitialPhase(hasValidToken: false, isRegistered: true);

        Assert.Equal(SetupPhase.Unauthenticated, result);
        Assert.Equal(SetupPhase.Unauthenticated, state.CurrentPhase);
        Assert.True(state.IsSetupRequired);
        Assert.False(state.IsAuthenticated);
    }

    [Fact]
    public void IsSetupRequired_false_when_complete()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);
        state.TransitionTo(SetupPhase.CertificateAcquired);
        state.TransitionTo(SetupPhase.Complete);

        Assert.False(state.IsSetupRequired);
    }

    [Fact]
    public void IsAuthenticated_false_when_unauthenticated()
    {
        SetupState state = new();

        Assert.False(state.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_true_when_authenticated()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);

        Assert.True(state.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_true_when_registered()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);

        Assert.True(state.IsAuthenticated);
    }
}
