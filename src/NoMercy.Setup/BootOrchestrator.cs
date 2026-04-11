using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;

namespace NoMercy.Setup;

public class BootOrchestrator
{
    private readonly SetupState _setupState;
    private readonly AuthManager _authManager;

    public BootOrchestrator(SetupState setupState, AuthManager authManager)
    {
        _setupState = setupState;
        _authManager = authManager;
    }

    /// <summary>
    /// Runs Phase 1 (essential, no network) and Phase 2 (auth).
    /// Returns true if setup mode is needed (interactive auth required).
    /// </summary>
    public async Task<bool> RunAsync(IServiceProvider services, CancellationToken ct)
    {
        // Phase 1: Essential tasks (blocking, no network)
        // Uses Start.cs as shim until Task 17 inlines task definitions
        Logger.Setup("Phase 1: Running essential tasks...");
        await Start.InitEssential([]);

        // Initialize TokenStore before any DB access that touches SecureValue
        Database.TokenStore.Initialize(services);

        // Phase 2: Authentication
        Logger.Setup("Phase 2: Authentication...");
        bool authSucceeded = await _authManager.InitializeAsync();

        if (authSucceeded)
        {
            // Check registration — if cert exists in DB, registration already happened
            // (cert is issued during registration). This survives process restarts
            // unlike the static Register.IsRegistered flag.
            bool isRegistered =
                Register.IsRegistered || Networking.Certificate.HasValidCertificate();
            _setupState.DetermineInitialPhase(hasValidToken: true, isRegistered: isRegistered);

            if (!isRegistered)
            {
                // Phase 3: Registration (blocking on first boot)
                await RunRegistrationAsync(ct);
            }

            // Phase 4: Background tasks (non-blocking)
            _authManager.ScheduleBackgroundRefresh(ct);
            _ = RunBackgroundTasksAsync(ct);

            return false; // No setup mode needed
        }

        // Auth failed — enter setup mode
        Logger.Setup("Interactive authentication required — entering setup mode");
        return true;
    }

    /// <summary>
    /// Called after interactive auth completes (setup flow).
    /// Waits for Authenticated state, runs registration, starts background tasks.
    /// Returns true if HTTPS restart is needed (cert was acquired).
    /// </summary>
    public async Task<bool> RunPostAuthAsync(CancellationToken ct)
    {
        Logger.Setup("Waiting for authentication to complete...");

        while (!_setupState.IsAuthenticated && !ct.IsCancellationRequested)
        {
            await _setupState.WaitForChangeAsync(ct);
        }

        if (ct.IsCancellationRequested)
            return false;

        Logger.Setup("Authentication complete — running registration...");

        // Phase 3: Registration + Certificate
        bool certAcquired = await RunRegistrationAsync(ct);

        // Phase 4: Background tasks
        _authManager.ScheduleBackgroundRefresh(ct);
        _ = RunBackgroundTasksAsync(ct);

        return certAcquired;
    }

    /// <summary>
    /// For headless/Docker environments: starts device code flow server-side.
    /// Does not block — runs in background. Completes when user authenticates
    /// via the device code, or when setup mode is exited by another path.
    /// </summary>
    public async Task StartHeadlessDeviceCodeFlowAsync(CancellationToken ct)
    {
        if (AuthManager.IsDesktopEnvironment())
            return;

        Logger.Setup("Headless environment detected — starting device code flow");

        try
        {
            string deviceEndpoint = $"{Config.AuthBaseUrl}protocol/openid-connect/auth/device";

            using HttpClient client = new();
            List<KeyValuePair<string, string>> body = AuthManager.BuildDeviceCodeRequestBody(
                Config.TokenClientId
            );

            using HttpResponseMessage response = await client.PostAsync(
                deviceEndpoint,
                new FormUrlEncodedContent(body)
            );

            if (!response.IsSuccessStatusCode)
            {
                Logger.Setup("Device code request failed", LogEventLevel.Warning);
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            DeviceAuthResponse? deviceResponse =
                Newtonsoft.Json.JsonConvert.DeserializeObject<DeviceAuthResponse>(json);

            if (deviceResponse is null)
            {
                Logger.Setup("Device code response could not be parsed", LogEventLevel.Warning);
                return;
            }

            string verificationUri = deviceResponse.VerificationUriComplete;
            string userCode = deviceResponse.UserCode;
            string deviceCode = deviceResponse.DeviceCode;
            int interval = deviceResponse.Interval > 0 ? deviceResponse.Interval : 5;

            if (!string.IsNullOrEmpty(verificationUri))
            {
                SetupTerminalUi ui = new();
                ui.Show(verificationUri, deviceResponse.VerificationUri, userCode, "");
            }

            if (!string.IsNullOrEmpty(deviceCode))
            {
                await PollDeviceGrant(deviceCode, interval, ct);
            }
        }
        catch (Exception ex)
        {
            Logger.Setup($"Device code flow error: {ex.Message}", LogEventLevel.Warning);
        }
    }

    private async Task<bool> RunRegistrationAsync(CancellationToken ct)
    {
        try
        {
            _setupState.TransitionTo(SetupPhase.Registering);
            _setupState.SetPhaseDetail("Registering server with NoMercy...");

            await Register.Init();

            _setupState.TransitionTo(SetupPhase.Registered);
            _setupState.SetPhaseDetail("Acquiring SSL certificate...");

            bool hasCert = Networking.Certificate.HasValidCertificate();

            if (hasCert)
                _setupState.TransitionTo(SetupPhase.CertificateAcquired);

            _setupState.TransitionTo(SetupPhase.Complete);
            Logger.Setup("Registration and certificate setup complete");

            return hasCert;
        }
        catch (Exception ex)
        {
            _setupState.SetError($"Registration failed: {ex.Message}");
            Logger.Setup($"Registration failed: {ex.Message}", LogEventLevel.Error);

            // Don't block — DegradedModeRecovery will retry
            _setupState.TransitionTo(SetupPhase.Complete);
            return false;
        }
    }

    private async Task RunBackgroundTasksAsync(CancellationToken ct)
    {
        try
        {
            Logger.Setup("Phase 4: Starting background tasks...");
            await Start.InitRemaining();
        }
        catch (Exception ex)
        {
            Logger.Setup($"Background tasks error: {ex.Message}", LogEventLevel.Warning);
        }
    }

    private async Task PollDeviceGrant(string deviceCode, int interval, CancellationToken ct)
    {
        string tokenEndpoint = $"{Config.AuthBaseUrl}protocol/openid-connect/token";

        while (!ct.IsCancellationRequested && !_setupState.IsAuthenticated)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);

            try
            {
                using HttpClient client = new();
                List<KeyValuePair<string, string>> body = AuthManager.BuildDeviceTokenBody(
                    Config.TokenClientId,
                    deviceCode
                );

                using HttpResponseMessage response = await client.PostAsync(
                    tokenEndpoint,
                    new FormUrlEncodedContent(body)
                );

                string json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    AuthResponse? tokens =
                        Newtonsoft.Json.JsonConvert.DeserializeObject<AuthResponse>(json);
                    if (tokens?.AccessToken != null)
                    {
                        await _authManager.StoreTokensAsync(tokens);
                        _setupState.TransitionTo(SetupPhase.Authenticating);
                        _setupState.TransitionTo(SetupPhase.Authenticated);
                        Logger.Setup("Device code authentication successful");
                        return;
                    }
                }

                dynamic? error = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                string? errorCode = error?.error?.ToString();
                if (errorCode is "expired_token" or "access_denied")
                {
                    Logger.Setup($"Device code flow ended: {errorCode}");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Setup($"Device poll error: {ex.Message}", LogEventLevel.Warning);
            }
        }
    }
}
