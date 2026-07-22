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
public class SetupStateTransitionsTests
{
    [Fact]
    public void InvalidTransition_Authenticating_to_Registered_fails()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        bool result = state.TransitionTo(targetPhase: SetupPhase.Registered);

        Assert.False(condition: result);
        Assert.Equal(expected: SetupPhase.Authenticating, actual: state.CurrentPhase);
    }

    [Fact]
    public void InvalidTransition_Authenticated_to_Registering_skipping_phases_fails()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);

        bool result = state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);

        Assert.False(condition: result);
        Assert.Equal(expected: SetupPhase.Authenticated, actual: state.CurrentPhase);
    }

    [Fact]
    public void InvalidTransition_Registered_to_Unauthenticated_backward_to_start_fails()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);

        bool result = state.TransitionTo(targetPhase: SetupPhase.Unauthenticated);

        Assert.False(condition: result);
        Assert.Equal(expected: SetupPhase.Registered, actual: state.CurrentPhase);
    }

    [Fact]
    public void InvalidTransition_Complete_to_Registered_backward_fails()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);
        state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
        state.TransitionTo(targetPhase: SetupPhase.Complete);

        bool result = state.TransitionTo(targetPhase: SetupPhase.Registered);

        Assert.False(condition: result);
        Assert.Equal(expected: SetupPhase.Complete, actual: state.CurrentPhase);
    }

    [Fact]
    public void ErrorRecoveryTransition_Authenticating_to_Unauthenticated_succeeds()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        bool result = state.TransitionTo(targetPhase: SetupPhase.Unauthenticated);

        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: state.CurrentPhase);
    }

    [Fact]
    public void ErrorRecoveryTransition_Registering_to_Authenticated_succeeds()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);

        bool result = state.TransitionTo(targetPhase: SetupPhase.Authenticated);

        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Authenticated, actual: state.CurrentPhase);
    }

    [Fact]
    public void RetryTransition_Authenticated_stays_Authenticated()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);

        bool result = state.TransitionTo(targetPhase: SetupPhase.Authenticated);

        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Authenticated, actual: state.CurrentPhase);
    }

    [Fact]
    public void RetryTransition_Registered_stays_Registered()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);

        bool result = state.TransitionTo(targetPhase: SetupPhase.Registered);

        Assert.True(condition: result);
        Assert.Equal(expected: SetupPhase.Registered, actual: state.CurrentPhase);
    }

    [Fact]
    public void SetError_preserves_current_phase()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        state.SetError(message: "Authentication failed");

        Assert.Equal(expected: SetupPhase.Authenticating, actual: state.CurrentPhase);
        Assert.Equal(expected: "Authentication failed", actual: state.ErrorMessage);
    }

    [Fact]
    public void ClearError_removes_error_message()
    {
        SetupState state = new();
        state.SetError(message: "Some error");

        state.ClearError();

        Assert.Null(@object: state.ErrorMessage);
    }

    [Fact]
    public void ErrorOnTransition_clears_previous_error()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.SetError(message: "Previous error");

        state.TransitionTo(targetPhase: SetupPhase.Authenticated);

        Assert.Null(@object: state.ErrorMessage);
    }

    [Fact]
    public void SetPhaseDetail_updates_detail()
    {
        SetupState state = new();

        state.SetPhaseDetail(detail: "Custom detail message");

        Assert.Equal(expected: "Custom detail message", actual: state.PhaseDetail);
    }

    [Fact]
    public void SetServerUrl_updates_url()
    {
        SetupState state = new();

        state.SetServerUrl(url: "https://nomercy.local:8080");

        Assert.Equal(expected: "https://nomercy.local:8080", actual: state.ServerUrl);
    }

    [Fact]
    public void Reset_returns_to_Unauthenticated()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.SetError(message: "Some error");

        state.Reset();

        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: state.CurrentPhase);
        Assert.Null(@object: state.ErrorMessage);
    }

    [Fact]
    public void DetermineInitialPhase_with_valid_token_and_registered_returns_complete()
    {
        SetupState state = new();

        SetupPhase result = state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);

        Assert.Equal(expected: SetupPhase.Complete, actual: result);
        Assert.Equal(expected: SetupPhase.Complete, actual: state.CurrentPhase);
        Assert.False(condition: state.IsSetupRequired);
    }

    [Fact]
    public void DetermineInitialPhase_with_valid_token_not_registered_returns_authenticated()
    {
        SetupState state = new();

        SetupPhase result = state.DetermineInitialPhase(hasValidToken: true, isRegistered: false);

        Assert.Equal(expected: SetupPhase.Authenticated, actual: result);
        Assert.Equal(expected: SetupPhase.Authenticated, actual: state.CurrentPhase);
        Assert.True(condition: state.IsSetupRequired);
        Assert.True(condition: state.IsAuthenticated);
    }

    [Fact]
    public void DetermineInitialPhase_without_valid_token_returns_unauthenticated()
    {
        SetupState state = new();

        SetupPhase result = state.DetermineInitialPhase(hasValidToken: false, isRegistered: true);

        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: result);
        Assert.Equal(expected: SetupPhase.Unauthenticated, actual: state.CurrentPhase);
        Assert.True(condition: state.IsSetupRequired);
        Assert.False(condition: state.IsAuthenticated);
    }

    [Fact]
    public void IsSetupRequired_false_when_complete()
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
    public void IsAuthenticated_false_when_unauthenticated()
    {
        SetupState state = new();

        Assert.False(condition: state.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_true_when_authenticated()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);

        Assert.True(condition: state.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_true_when_registered()
    {
        SetupState state = new();
        state.TransitionTo(targetPhase: SetupPhase.Authenticating);
        state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        state.TransitionTo(targetPhase: SetupPhase.Registering);
        state.TransitionTo(targetPhase: SetupPhase.Registered);

        Assert.True(condition: state.IsAuthenticated);
    }
}
