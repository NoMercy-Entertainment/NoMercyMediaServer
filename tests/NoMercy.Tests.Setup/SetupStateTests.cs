using NoMercy.Setup;

namespace NoMercy.Tests.Setup;

public class SetupStateTests
{
    // --- Initial State ---

    [Fact]
    public void NewState_StartsAsUnauthenticated()
    {
        SetupState state = new();
        Assert.Equal(SetupPhase.Unauthenticated, state.CurrentPhase);
    }

    [Fact]
    public void NewState_IsSetupRequired()
    {
        SetupState state = new();
        Assert.True(state.IsSetupRequired);
    }

    [Fact]
    public void NewState_IsNotAuthenticated()
    {
        SetupState state = new();
        Assert.False(state.IsAuthenticated);
    }

    [Fact]
    public void NewState_HasNoError()
    {
        SetupState state = new();
        Assert.Null(state.ErrorMessage);
    }

    // --- Forward Transitions ---

    [Fact]
    public void TransitionTo_Authenticating_FromUnauthenticated_Succeeds()
    {
        SetupState state = new();
        bool result = state.TransitionTo(SetupPhase.Authenticating);
        Assert.True(result);
        Assert.Equal(SetupPhase.Authenticating, state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Authenticated_FromAuthenticating_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        bool result = state.TransitionTo(SetupPhase.Authenticated);
        Assert.True(result);
        Assert.Equal(SetupPhase.Authenticated, state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Registering_FromAuthenticated_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        bool result = state.TransitionTo(SetupPhase.Registering);
        Assert.True(result);
        Assert.Equal(SetupPhase.Registering, state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Registered_FromRegistering_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        bool result = state.TransitionTo(SetupPhase.Registered);
        Assert.True(result);
        Assert.Equal(SetupPhase.Registered, state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_CertificateAcquired_FromRegistered_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);
        bool result = state.TransitionTo(SetupPhase.CertificateAcquired);
        Assert.True(result);
        Assert.Equal(SetupPhase.CertificateAcquired, state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Complete_FromCertificateAcquired_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);
        state.TransitionTo(SetupPhase.CertificateAcquired);
        bool result = state.TransitionTo(SetupPhase.Complete);
        Assert.True(result);
        Assert.Equal(SetupPhase.Complete, state.CurrentPhase);
    }

    [Fact]
    public void Complete_IsNotSetupRequired()
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
    public void Authenticated_IsAuthenticated()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        Assert.True(state.IsAuthenticated);
    }

    // --- Invalid Transitions ---

    [Fact]
    public void TransitionTo_Complete_FromUnauthenticated_Fails()
    {
        SetupState state = new();
        bool result = state.TransitionTo(SetupPhase.Complete);
        Assert.False(result);
        Assert.Equal(SetupPhase.Unauthenticated, state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Registered_FromUnauthenticated_Fails()
    {
        SetupState state = new();
        bool result = state.TransitionTo(SetupPhase.Registered);
        Assert.False(result);
        Assert.Equal(SetupPhase.Unauthenticated, state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Authenticated_FromUnauthenticated_Fails()
    {
        SetupState state = new();
        bool result = state.TransitionTo(SetupPhase.Authenticated);
        Assert.False(result);
        Assert.Equal(SetupPhase.Unauthenticated, state.CurrentPhase);
    }

    // --- Error Recovery Transitions ---

    [Fact]
    public void TransitionTo_Unauthenticated_FromAuthenticating_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        bool result = state.TransitionTo(SetupPhase.Unauthenticated);
        Assert.True(result);
        Assert.Equal(SetupPhase.Unauthenticated, state.CurrentPhase);
    }

    [Fact]
    public void TransitionTo_Authenticated_FromRegistering_Succeeds()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        bool result = state.TransitionTo(SetupPhase.Authenticated);
        Assert.True(result);
        Assert.Equal(SetupPhase.Authenticated, state.CurrentPhase);
    }

    // --- Error Handling ---

    [Fact]
    public void SetError_StoresMessage()
    {
        SetupState state = new();
        state.SetError("Network timeout");
        Assert.Equal("Network timeout", state.ErrorMessage);
    }

    [Fact]
    public void TransitionTo_ClearsError()
    {
        SetupState state = new();
        state.SetError("Some error");
        state.TransitionTo(SetupPhase.Authenticating);
        Assert.Null(state.ErrorMessage);
    }

    // --- Reset ---

    [Fact]
    public void Reset_ReturnsToUnauthenticated()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.SetError("some error");
        state.Reset();
        Assert.Equal(SetupPhase.Unauthenticated, state.CurrentPhase);
        Assert.Null(state.ErrorMessage);
    }

    // --- IsValidTransition ---

    [Theory]
    [InlineData(SetupPhase.Unauthenticated, SetupPhase.Authenticating, true)]
    [InlineData(SetupPhase.Authenticating, SetupPhase.Authenticated, true)]
    [InlineData(SetupPhase.Authenticated, SetupPhase.Registering, true)]
    [InlineData(SetupPhase.Registering, SetupPhase.Registered, true)]
    [InlineData(SetupPhase.Registered, SetupPhase.CertificateAcquired, true)]
    [InlineData(SetupPhase.CertificateAcquired, SetupPhase.Complete, true)]
    [InlineData(SetupPhase.Authenticating, SetupPhase.Unauthenticated, true)]
    [InlineData(SetupPhase.Registering, SetupPhase.Authenticated, true)]
    [InlineData(SetupPhase.Unauthenticated, SetupPhase.Complete, false)]
    [InlineData(SetupPhase.Unauthenticated, SetupPhase.Registered, false)]
    [InlineData(SetupPhase.Complete, SetupPhase.Unauthenticated, false)]
    public void IsValidTransition_ReturnsExpected(SetupPhase from, SetupPhase to, bool expected)
    {
        Assert.Equal(expected, SetupState.IsValidTransition(from, to));
    }

    // --- WaitForChangeAsync ---

    [Fact]
    public async Task WaitForChangeAsync_CompletesOnTransition()
    {
        SetupState state = new();
        Task waitTask = state.WaitForChangeAsync();

        Assert.False(waitTask.IsCompleted);

        state.TransitionTo(SetupPhase.Authenticating);

        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_CompletesOnSetError()
    {
        SetupState state = new();
        Task waitTask = state.WaitForChangeAsync();

        Assert.False(waitTask.IsCompleted);

        state.SetError("test error");

        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_CompletesOnReset()
    {
        SetupState state = new();
        state.TransitionTo(SetupPhase.Authenticating);

        Task waitTask = state.WaitForChangeAsync();
        Assert.False(waitTask.IsCompleted);

        state.Reset();

        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_SupportsMultipleWaiters()
    {
        SetupState state = new();
        Task wait1 = state.WaitForChangeAsync();
        Task wait2 = state.WaitForChangeAsync();

        state.TransitionTo(SetupPhase.Authenticating);

        await Task.WhenAll(
            wait1.WaitAsync(TimeSpan.FromSeconds(1)),
            wait2.WaitAsync(TimeSpan.FromSeconds(1))
        );

        Assert.True(wait1.IsCompleted);
        Assert.True(wait2.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_CanBeCalledAgainAfterChange()
    {
        SetupState state = new();

        Task wait1 = state.WaitForChangeAsync();
        state.TransitionTo(SetupPhase.Authenticating);
        await wait1.WaitAsync(TimeSpan.FromSeconds(1));

        Task wait2 = state.WaitForChangeAsync();
        Assert.False(wait2.IsCompleted);

        state.TransitionTo(SetupPhase.Authenticated);
        await wait2.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(wait2.IsCompleted);
    }

    [Fact]
    public async Task WaitForChangeAsync_RespectsCancellation()
    {
        SetupState state = new();
        using CancellationTokenSource cts = new();

        Task waitTask = state.WaitForChangeAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            waitTask.WaitAsync(TimeSpan.FromSeconds(1))
        );
    }

    // --- WaitForSetupCompleteAsync ---

    [Fact]
    public async Task WaitForSetupCompleteAsync_CompletesWhenTransitionedToComplete()
    {
        SetupState state = new();
        Task waitTask = state.WaitForSetupCompleteAsync();

        Assert.False(waitTask.IsCompleted);

        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);
        state.TransitionTo(SetupPhase.CertificateAcquired);

        Assert.False(waitTask.IsCompleted);

        state.TransitionTo(SetupPhase.Complete);

        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_CompletesImmediatelyWhenAlreadyComplete()
    {
        SetupState state = new();
        state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);

        Task waitTask = state.WaitForSetupCompleteAsync();

        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_RespectsCancellation()
    {
        SetupState state = new();
        using CancellationTokenSource cts = new();

        Task waitTask = state.WaitForSetupCompleteAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            waitTask.WaitAsync(TimeSpan.FromSeconds(1))
        );
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_SupportsMultipleWaiters()
    {
        SetupState state = new();
        Task wait1 = state.WaitForSetupCompleteAsync();
        Task wait2 = state.WaitForSetupCompleteAsync();

        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);
        state.TransitionTo(SetupPhase.CertificateAcquired);
        state.TransitionTo(SetupPhase.Complete);

        await Task.WhenAll(
            wait1.WaitAsync(TimeSpan.FromSeconds(1)),
            wait2.WaitAsync(TimeSpan.FromSeconds(1))
        );

        Assert.True(wait1.IsCompleted);
        Assert.True(wait2.IsCompleted);
    }

    [Fact]
    public async Task WaitForSetupCompleteAsync_DoesNotCompleteOnIntermediatePhases()
    {
        SetupState state = new();
        Task waitTask = state.WaitForSetupCompleteAsync();

        state.TransitionTo(SetupPhase.Authenticating);
        state.TransitionTo(SetupPhase.Authenticated);
        state.TransitionTo(SetupPhase.Registering);
        state.TransitionTo(SetupPhase.Registered);
        state.TransitionTo(SetupPhase.CertificateAcquired);

        // Give it a moment to ensure it doesn't complete prematurely
        await Task.Delay(50);
        Assert.False(waitTask.IsCompleted);
    }

    // --- DetermineInitialPhase ---

    [Fact]
    public void DetermineInitialPhase_ValidTokenRegistered_SetsComplete()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(hasValidToken: true, isRegistered: true);
        Assert.Equal(SetupPhase.Complete, phase);
        Assert.Equal(SetupPhase.Complete, state.CurrentPhase);
    }

    [Fact]
    public void DetermineInitialPhase_ValidTokenNotRegistered_SetsAuthenticated()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(hasValidToken: true, isRegistered: false);
        Assert.Equal(SetupPhase.Authenticated, phase);
    }

    [Fact]
    public void DetermineInitialPhase_NoToken_StaysUnauthenticated()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(hasValidToken: false);
        Assert.Equal(SetupPhase.Unauthenticated, phase);
    }
}
