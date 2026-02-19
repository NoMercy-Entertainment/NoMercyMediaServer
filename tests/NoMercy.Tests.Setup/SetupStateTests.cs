using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Newtonsoft.Json;
using NoMercy.Setup;
using NoMercy.Setup.Dto;

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
    public void IsValidTransition_ReturnsExpected(
        SetupPhase from, SetupPhase to, bool expected)
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
            wait2.WaitAsync(TimeSpan.FromSeconds(1)));

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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => waitTask.WaitAsync(TimeSpan.FromSeconds(1)));
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
        state.DetermineInitialPhase(TokenState.Valid);

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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => waitTask.WaitAsync(TimeSpan.FromSeconds(1)));
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
            wait2.WaitAsync(TimeSpan.FromSeconds(1)));

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
    public void DetermineInitialPhase_ValidToken_SetsComplete()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(TokenState.Valid);
        Assert.Equal(SetupPhase.Complete, phase);
        Assert.Equal(SetupPhase.Complete, state.CurrentPhase);
    }

    [Fact]
    public void DetermineInitialPhase_NoRefreshToken_SetsComplete()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(TokenState.NoRefreshToken);
        Assert.Equal(SetupPhase.Complete, phase);
    }

    [Fact]
    public void DetermineInitialPhase_ExpiredToken_SetsUnauthenticated()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(TokenState.Expired);
        Assert.Equal(SetupPhase.Unauthenticated, phase);
    }

    [Fact]
    public void DetermineInitialPhase_MissingToken_SetsUnauthenticated()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(TokenState.Missing);
        Assert.Equal(SetupPhase.Unauthenticated, phase);
    }

    [Fact]
    public void DetermineInitialPhase_CorruptToken_SetsUnauthenticated()
    {
        SetupState state = new();
        SetupPhase phase = state.DetermineInitialPhase(TokenState.Corrupt);
        Assert.Equal(SetupPhase.Unauthenticated, phase);
    }
}

public class TokenValidationTests : IDisposable
{
    private readonly string _tempDir;

    public TokenValidationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nomercy_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CreateTokenFile(string content)
    {
        string filePath = Path.Combine(_tempDir, "token.json");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private static string CreateValidJwt(DateTime validTo)
    {
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken token = new(
            issuer: "test",
            audience: "test",
            claims: [new("sub", "user1")],
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: validTo);
        return handler.WriteToken(token);
    }

    [Fact]
    public async Task ValidateTokenFile_MissingFile_ReturnsMissing()
    {
        string path = Path.Combine(_tempDir, "nonexistent.json");
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Missing, result);
    }

    [Fact]
    public async Task ValidateTokenFile_EmptyFile_ReturnsMissing()
    {
        string path = CreateTokenFile("");
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Missing, result);
    }

    [Fact]
    public async Task ValidateTokenFile_EmptyJson_ReturnsMissing()
    {
        string path = CreateTokenFile("{}");
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Missing, result);
    }

    [Fact]
    public async Task ValidateTokenFile_InvalidJson_ReturnsCorrupt()
    {
        string path = CreateTokenFile("not valid json {{{");
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Corrupt, result);
    }

    [Fact]
    public async Task ValidateTokenFile_NullAccessToken_ReturnsMissing()
    {
        AuthResponse data = new() { RefreshToken = "refresh" };
        string path = CreateTokenFile(JsonConvert.SerializeObject(data));
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Missing, result);
    }

    [Fact]
    public async Task ValidateTokenFile_MalformedJwt_ReturnsCorrupt()
    {
        AuthResponse data = new() { AccessToken = "not.a.valid-jwt-token" };
        string path = CreateTokenFile(JsonConvert.SerializeObject(data));
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Corrupt, result);
    }

    [Fact]
    public async Task ValidateTokenFile_JwtWrongPartCount_ReturnsCorrupt()
    {
        AuthResponse data = new() { AccessToken = "only-one-part" };
        string path = CreateTokenFile(JsonConvert.SerializeObject(data));
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Corrupt, result);
    }

    [Fact]
    public async Task ValidateTokenFile_ExpiredJwt_ReturnsExpired()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddDays(1));
        AuthResponse data = new() { AccessToken = jwt, RefreshToken = "refresh" };
        string path = CreateTokenFile(JsonConvert.SerializeObject(data));
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Expired, result);
    }

    [Fact]
    public async Task ValidateTokenFile_ValidJwt_NoRefreshToken_ReturnsNoRefreshToken()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddDays(30));
        AuthResponse data = new() { AccessToken = jwt };
        string path = CreateTokenFile(JsonConvert.SerializeObject(data));
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.NoRefreshToken, result);
    }

    [Fact]
    public async Task ValidateTokenFile_ValidJwt_WithRefreshToken_ReturnsValid()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddDays(30));
        AuthResponse data = new() { AccessToken = jwt, RefreshToken = "refresh-token" };
        string path = CreateTokenFile(JsonConvert.SerializeObject(data));
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Valid, result);
    }

    [Fact]
    public async Task ValidateTokenFile_JwtExpiringIn4Days_ReturnsExpired()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddDays(4));
        AuthResponse data = new() { AccessToken = jwt, RefreshToken = "refresh-token" };
        string path = CreateTokenFile(JsonConvert.SerializeObject(data));
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Expired, result);
    }

    [Fact]
    public async Task ValidateTokenFile_JwtExpiringIn6Days_ReturnsValid()
    {
        string jwt = CreateValidJwt(DateTime.UtcNow.AddDays(6));
        AuthResponse data = new() { AccessToken = jwt, RefreshToken = "refresh-token" };
        string path = CreateTokenFile(JsonConvert.SerializeObject(data));
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Valid, result);
    }

    [Fact]
    public async Task ValidateTokenFile_WhitespaceOnly_ReturnsMissing()
    {
        string path = CreateTokenFile("   \n\t  ");
        TokenState result = await SetupState.ValidateTokenFile(path);
        Assert.Equal(TokenState.Missing, result);
    }
}
