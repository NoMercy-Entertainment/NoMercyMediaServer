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

using Newtonsoft.Json;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Security;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Dto;
using NoMercy.Setup.Server;
using NoMercy.Setup.Ui;
using Serilog.Events;

namespace NoMercy.Setup.Boot;

public class BootOrchestrator
{
    private readonly SetupState _setupState;
    private readonly AuthManager _authManager;
    private readonly IApiKeyLoader _apiKeyLoader;
    private readonly IDegradedModeRecovery _degradedModeRecovery;
    private readonly IServerRegistrationService _serverRegistrationService;

    private readonly IAuthTokenStore _authTokenStore;
    private readonly ICertificateService _certificateService;

    public BootOrchestrator(
        SetupState setupState,
        AuthManager authManager,
        IApiKeyLoader apiKeyLoader,
        IDegradedModeRecovery degradedModeRecovery,
        IServerRegistrationService serverRegistrationService,
        IAuthTokenStore authTokenStore,
        ICertificateService certificateService
    )
    {
        _authTokenStore = authTokenStore;
        _certificateService = certificateService;
        _setupState = setupState;
        _authManager = authManager;
        _apiKeyLoader = apiKeyLoader;
        _degradedModeRecovery = degradedModeRecovery;
        _serverRegistrationService = serverRegistrationService;
    }

    /// <summary>
    /// Runs Phase 1 (essential, no network) and Phase 2 (auth).
    /// Returns true if setup mode is needed (interactive auth required).
    /// </summary>
    public async Task<bool> RunAsync(IServiceProvider services, CancellationToken ct)
    {
        // Phase 1: Essential tasks (blocking, no network)
        // Uses Start.cs as shim until Task 17 inlines task definitions
        Logger.Setup(message: "Phase 1: Running essential tasks...");
        await Start.InitEssential();

        await _apiKeyLoader.LoadKeys(ct: ct);

        // Initialize TokenStore before any DB access that touches SecureValue
        TokenStore.Initialize(serviceProvider: services);

        // Load SSL certificate into memory cache (from DB or legacy PEM files)
        try
        {
            _certificateService.LoadFromDb();
        }
        catch (Exception ex)
        {
            Logger.Setup(message: $"Certificate pre-load skipped: {ex.Message}", level: LogEventLevel.Verbose);
        }

        // Phase 2: Authentication
        Logger.Setup(message: "Phase 2: Authentication...");
        await CheckKeycloakReachabilityAsync();
        bool authSucceeded = await _authManager.InitializeAsync();

        if (authSucceeded)
        {
            NmSystem.Lifecycle.ServerPhaseTracker.Current?.MarkComplete(
                stage: NmSystem.Lifecycle.BootStage.Auth
            );

            bool isRegistered = _certificateService.HasValidCertificate();
            _setupState.DetermineInitialPhase(hasValidToken: true, isRegistered: isRegistered);

            if (isRegistered)
            {
                NmSystem.Lifecycle.ServerPhaseTracker.Current?.MarkComplete(
                    stage: NmSystem.Lifecycle.BootStage.Registered
                );
            }
            else
            {
                // Phase 3: Registration (blocking on first boot)
                await RunRegistrationAsync(ct: ct);
            }

            // Phase 4: Background tasks (non-blocking)
            _authManager.ScheduleBackgroundRefresh(ct: ct);
            _ = RunBackgroundTasksAsync(ct: ct);

            return false; // No setup mode needed
        }

        // Auth failed — enter setup mode
        Logger.Setup(message: "Interactive authentication required — entering setup mode");
        return true;
    }

    /// <summary>
    /// Called after interactive auth completes (setup flow).
    /// Waits for Authenticated state, runs registration, starts background tasks.
    /// Returns true if HTTPS restart is needed (cert was acquired).
    /// </summary>
    public async Task<bool> RunPostAuthAsync(CancellationToken ct)
    {
        Logger.Setup(message: "Waiting for authentication to complete...");

        while (!_setupState.IsAuthenticated && !ct.IsCancellationRequested)
        {
            await _setupState.WaitForChangeAsync(cancellationToken: ct);
        }

        if (ct.IsCancellationRequested)
            return false;

        Logger.Setup(message: "Authentication complete — running registration...");
        NmSystem.Lifecycle.ServerPhaseTracker.Current?.MarkComplete(
            stage: NmSystem.Lifecycle.BootStage.Auth
        );

        // Phase 3: Registration + Certificate
        bool certAcquired = await RunRegistrationAsync(ct: ct);

        // Phase 4: Background tasks
        _authManager.ScheduleBackgroundRefresh(ct: ct);
        _ = RunBackgroundTasksAsync(ct: ct);

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

        Logger.Setup(message: "Headless environment detected — starting device code flow");

        try
        {
            string deviceEndpoint =
                $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/auth/device";

            using HttpClient client = new();
            List<KeyValuePair<string, string>> body = AuthManager.BuildDeviceCodeRequestBody(
                clientId: ExternalServicesConfig.Current.TokenClientId
            );

            using HttpResponseMessage response = await client.PostAsync(
                requestUri: deviceEndpoint,
                content: new FormUrlEncodedContent(nameValueCollection: body)
            );

            if (!response.IsSuccessStatusCode)
            {
                Logger.Setup(message: "Device code request failed", level: LogEventLevel.Warning);
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            DeviceAuthResponse? deviceResponse = JsonConvert.DeserializeObject<DeviceAuthResponse>(
                value: json
            );

            if (deviceResponse is null)
            {
                Logger.Setup(message: "Device code response could not be parsed", level: LogEventLevel.Warning);
                return;
            }

            string verificationUri = deviceResponse.VerificationUriComplete;
            string userCode = deviceResponse.UserCode;
            string deviceCode = deviceResponse.DeviceCode;
            int interval = deviceResponse.Interval > 0 ? deviceResponse.Interval : 5;

            if (!string.IsNullOrEmpty(value: verificationUri))
            {
                SetupTerminalUi ui = new();
                ui.Show(verificationUriComplete: verificationUri, verificationUri: deviceResponse.VerificationUri, userCode: userCode, setupPageUrl: "");
            }

            if (!string.IsNullOrEmpty(value: deviceCode))
            {
                await PollDeviceGrant(deviceCode: deviceCode, interval: interval, ct: ct);
            }
        }
        catch (Exception ex)
        {
            Logger.Setup(message: $"Device code flow error: {ex.Message}", level: LogEventLevel.Warning);
        }
    }

    /// <summary>
    /// Probes the Keycloak well-known config endpoint and logs an explicit error when it is
    /// unreachable AND no cached JWKS key is available. Failures are never swallowed silently.
    /// </summary>
    private async Task CheckKeycloakReachabilityAsync()
    {
        string wellKnown =
            $"{ExternalServicesConfig.Current.AuthBaseUrl}.well-known/openid-configuration";

        try
        {
            using HttpClient client = new();
            client.Timeout = TimeSpan.FromSeconds(seconds: 10);
            client.WithNoMercyUserAgent();

            using HttpResponseMessage response = await client.GetAsync(requestUri: wellKnown);

            if (response.IsSuccessStatusCode)
            {
                Logger.Setup(message: $"Keycloak reachable at {ExternalServicesConfig.Current.AuthBaseUrl}");
            }
            else
            {
                Logger.Setup(
                    message: $"Keycloak returned {(int)response.StatusCode} from {wellKnown} — auth may degrade",
                    level: LogEventLevel.Warning
                );
            }
        }
        catch (Exception ex)
        {
            bool hasCachedKey = OfflineJwksCache.CachedSigningKey is not null;

            if (hasCachedKey)
            {
                Logger.Setup(
                    message: $"Keycloak unreachable ({ex.Message}) — offline JWKS cache is present; JWT validation will use cached keys",
                    level: LogEventLevel.Warning
                );
            }
            else
            {
                Logger.Setup(
                    message: $"BOOT FAILURE: Keycloak unreachable at {ExternalServicesConfig.Current.AuthBaseUrl} and no cached JWKS key found. "
                             + $"Cause: {ex.Message}. "
                             + $"The server cannot validate JWTs. Complete setup at /setup or ensure Keycloak is reachable before restarting.",
                    level: LogEventLevel.Error
                );
            }
        }
    }

    public async Task<bool> RunRegistrationAsync(CancellationToken ct)
    {
        try
        {
            _setupState.TransitionTo(targetPhase: SetupPhase.Registering);
            _setupState.SetPhaseDetail(detail: "Registering server with NoMercy...");

            await _serverRegistrationService.Init();

            _setupState.TransitionTo(targetPhase: SetupPhase.Registered);
            _setupState.SetPhaseDetail(detail: "Acquiring SSL certificate...");

            bool hasCert = _certificateService.HasValidCertificate();

            if (hasCert)
                _setupState.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);

            _setupState.TransitionTo(targetPhase: SetupPhase.Complete);
            Logger.Setup(message: "Registration and certificate setup complete");

            NmSystem.Lifecycle.ServerPhaseTracker.Current?.MarkComplete(
                stage: NmSystem.Lifecycle.BootStage.Registered
            );

            return hasCert;
        }
        catch (Exception ex)
        {
            _setupState.SetError(message: $"Registration failed: {ex.Message}");
            Logger.Setup(message: $"Registration failed: {ex.Message}", level: LogEventLevel.Error);

            // Don't block — DegradedModeRecovery will retry. Mark Registered as
            // complete so workers don't block forever on a known-degraded boot —
            // partial functionality beats no functionality, and the recovery loop
            // will quietly retry registration in the background.
            NmSystem.Lifecycle.ServerPhaseTracker.Current?.MarkComplete(
                stage: NmSystem.Lifecycle.BootStage.Registered
            );

            _setupState.TransitionTo(targetPhase: SetupPhase.Complete);
            return false;
        }
    }

    private async Task RunBackgroundTasksAsync(CancellationToken ct)
    {
        try
        {
            Logger.Setup(message: "Phase 4: Starting background tasks...");
            await Start.InitRemaining(recovery: _degradedModeRecovery, accessToken: _authTokenStore.AccessToken);
        }
        catch (Exception ex)
        {
            Logger.Setup(message: $"Background tasks error: {ex.Message}", level: LogEventLevel.Warning);
        }
    }

    private async Task PollDeviceGrant(string deviceCode, int interval, CancellationToken ct)
    {
        string tokenEndpoint =
            $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/token";

        while (!ct.IsCancellationRequested && !_setupState.IsAuthenticated)
        {
            await Task.Delay(delay: TimeSpan.FromSeconds(seconds: interval), cancellationToken: ct);

            try
            {
                using HttpClient client = new();
                List<KeyValuePair<string, string>> body = AuthManager.BuildDeviceTokenBody(
                    clientId: ExternalServicesConfig.Current.TokenClientId,
                    deviceCode: deviceCode
                );

                using HttpResponseMessage response = await client.PostAsync(
                    requestUri: tokenEndpoint,
                    content: new FormUrlEncodedContent(nameValueCollection: body)
                );

                string json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    AuthResponse? tokens = JsonConvert.DeserializeObject<AuthResponse>(value: json);
                    if (tokens?.AccessToken != null)
                    {
                        await _authManager.StoreTokensAsync(tokens: tokens);
                        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticating);
                        _setupState.TransitionTo(targetPhase: SetupPhase.Authenticated);
                        Logger.Setup(message: "Device code authentication successful");
                        return;
                    }
                }

                dynamic? error = JsonConvert.DeserializeObject(value: json);
                string? errorCode = error?.error?.ToString();
                if (errorCode is "expired_token" or "access_denied")
                {
                    Logger.Setup(message: $"Device code flow ended: {errorCode}");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Setup(message: $"Device poll error: {ex.Message}", level: LogEventLevel.Warning);
            }
        }
    }
}
