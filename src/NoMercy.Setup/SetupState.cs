using System.IdentityModel.Tokens.Jwt;
using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;

namespace NoMercy.Setup;

public enum SetupPhase
{
    Unauthenticated,
    Authenticating,
    Authenticated,
    Registering,
    Registered,
    CertificateAcquired,
    Complete
}

public enum TokenState
{
    Valid,
    Expired,
    Missing,
    Corrupt,
    NoRefreshToken
}

public class SetupState
{
    private readonly object _lock = new();

    private SetupPhase _currentPhase = SetupPhase.Unauthenticated;
    private string? _errorMessage;
    private TaskCompletionSource _changeSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _setupCompletedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SetupPhase CurrentPhase
    {
        get { lock (_lock) return _currentPhase; }
    }

    public string? ErrorMessage
    {
        get { lock (_lock) return _errorMessage; }
    }

    public bool IsSetupRequired => CurrentPhase < SetupPhase.Complete;

    public bool IsAuthenticated => CurrentPhase >= SetupPhase.Authenticated;

    public Task WaitForChangeAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource signal;
        lock (_lock)
        {
            signal = _changeSignal;
        }

        return signal.Task.WaitAsync(cancellationToken);
    }

    public Task WaitForSetupCompleteAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource signal;
        lock (_lock)
        {
            if (_currentPhase >= SetupPhase.Complete)
                return Task.CompletedTask;
            signal = _setupCompletedSignal;
        }

        return signal.Task.WaitAsync(cancellationToken);
    }

    public bool TransitionTo(SetupPhase targetPhase)
    {
        lock (_lock)
        {
            if (!IsValidTransition(_currentPhase, targetPhase))
            {
                Logger.Setup(
                    $"Invalid setup transition: {_currentPhase} → {targetPhase}",
                    LogEventLevel.Warning);
                return false;
            }

            SetupPhase previousPhase = _currentPhase;
            _currentPhase = targetPhase;
            _errorMessage = null;

            Logger.Setup($"Setup phase: {previousPhase} → {targetPhase}");
            NotifyChange();

            if (targetPhase == SetupPhase.Complete)
                _setupCompletedSignal.TrySetResult();

            return true;
        }
    }

    public void SetError(string message)
    {
        lock (_lock)
        {
            _errorMessage = message;
            Logger.Setup($"Setup error in {_currentPhase}: {message}", LogEventLevel.Error);
            NotifyChange();
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _currentPhase = SetupPhase.Unauthenticated;
            _errorMessage = null;
            NotifyChange();
        }
    }

    private void NotifyChange()
    {
        TaskCompletionSource previous = _changeSignal;
        _changeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        previous.TrySetResult();
    }

    internal static bool IsValidTransition(SetupPhase from, SetupPhase to)
    {
        return (from, to) switch
        {
            // Forward transitions
            (SetupPhase.Unauthenticated, SetupPhase.Authenticating) => true,
            (SetupPhase.Authenticating, SetupPhase.Authenticated) => true,
            (SetupPhase.Authenticated, SetupPhase.Registering) => true,
            (SetupPhase.Registering, SetupPhase.Registered) => true,
            (SetupPhase.Registered, SetupPhase.CertificateAcquired) => true,
            (SetupPhase.CertificateAcquired, SetupPhase.Complete) => true,

            // Error recovery: authenticating can fail back to unauthenticated
            (SetupPhase.Authenticating, SetupPhase.Unauthenticated) => true,
            // Registering can fail back to authenticated (retry registration)
            (SetupPhase.Registering, SetupPhase.Authenticated) => true,
            // Certificate failure can go back to registered (retry cert)
            (SetupPhase.Registered, SetupPhase.Registered) => true,

            _ => false
        };
    }

    public static async Task<TokenState> ValidateTokenFile()
    {
        return await ValidateTokenFile(AppFiles.TokenFile);
    }

    internal static async Task<TokenState> ValidateTokenFile(string tokenFilePath)
    {
        if (!File.Exists(tokenFilePath))
            return TokenState.Missing;

        string json;
        try
        {
            json = await File.ReadAllTextAsync(tokenFilePath);
        }
        catch
        {
            return TokenState.Corrupt;
        }

        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
            return TokenState.Missing;

        AuthResponse? tokenData;
        try
        {
            tokenData = JsonConvert.DeserializeObject<AuthResponse>(json);
        }
        catch
        {
            return TokenState.Corrupt;
        }

        if (string.IsNullOrEmpty(tokenData?.AccessToken))
            return TokenState.Missing;

        string[] parts = tokenData.AccessToken.Split('.');
        if (parts.Length != 3)
            return TokenState.Corrupt;

        try
        {
            JwtSecurityTokenHandler handler = new();
            JwtSecurityToken jwt = handler.ReadJwtToken(tokenData.AccessToken);

            if (jwt.ValidTo < DateTime.UtcNow.AddDays(5))
                return TokenState.Expired;
        }
        catch
        {
            return TokenState.Corrupt;
        }

        if (string.IsNullOrEmpty(tokenData.RefreshToken))
            return TokenState.NoRefreshToken;

        return TokenState.Valid;
    }

    public SetupPhase DetermineInitialPhase(TokenState tokenState)
    {
        lock (_lock)
        {
            _currentPhase = tokenState switch
            {
                TokenState.Valid => SetupPhase.Complete,
                TokenState.NoRefreshToken => SetupPhase.Complete,
                TokenState.Expired => SetupPhase.Unauthenticated,
                TokenState.Missing => SetupPhase.Unauthenticated,
                TokenState.Corrupt => SetupPhase.Unauthenticated,
                _ => SetupPhase.Unauthenticated
            };

            if (tokenState == TokenState.NoRefreshToken)
            {
                Logger.Setup(
                    "Token valid but no refresh token — will need re-auth later",
                    LogEventLevel.Warning);
            }

            if (tokenState == TokenState.Corrupt)
            {
                Logger.Setup(
                    "Token file is corrupted — entering setup mode",
                    LogEventLevel.Warning);
            }

            return _currentPhase;
        }
    }
}
