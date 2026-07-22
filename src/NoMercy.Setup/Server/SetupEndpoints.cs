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

using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Auth;
using NoMercy.Setup.Boot;
using NoMercy.Setup.Dto;
using NoMercy.Setup.Ui;
using NoMercyQueue.Workers;
using QRCoder;
using Serilog.Events;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Setup.Server;

/// <summary>
/// Request handlers for the setup flow. Designed to be called by middleware —
/// no standalone WebApplication. PKCE state is managed per-flow and regenerated
/// after each exchange. SetupState and AuthManager come from DI; never replaced
/// with <c>new()</c>.
/// </summary>
public class SetupEndpoints
{
    private readonly SetupState _state;
    private readonly AuthManager _authManager;
    private readonly SetupTerminalUi? _terminalUi;

    private readonly object _pkceLock = new();
    private string _codeVerifier;
    private string _codeChallenge;
    private string _pkceState;
    private bool _exchangeCompleted;

    private readonly IServerRegistrationService _serverRegistrationService;

    public SetupEndpoints(
        SetupState state,
        AuthManager authManager,
        IServerRegistrationService serverRegistrationService
    )
    {
        _state = state;
        _authManager = authManager;
        _serverRegistrationService = serverRegistrationService;

        _terminalUi = SetupTerminalUi.IsInteractiveTerminal ? new SetupTerminalUi() : null;

        _codeVerifier = AuthManager.GenerateCodeVerifier();
        _codeChallenge = AuthManager.GenerateCodeChallenge(codeVerifier: _codeVerifier);
        _pkceState = Guid.NewGuid().ToString(format: "N");
        _exchangeCompleted = false;
    }

    // ── Dispatcher ──────────────────────────────────────────────────────────

    public async Task HandleRequestAsync(HttpContext context)
    {
        string path = (context.Request.Path.Value?.TrimEnd(trimChar: '/') ?? string.Empty).ToLowerInvariant();

        switch (path)
        {
            case "/setup":
                await HandleSetupPage(context: context);
                break;

            case "/setup/setup.css":
                await HandleEmbeddedResource(context: context, filename: "setup.css", contentType: "text/css");
                break;

            case "/setup/setup.js":
                await HandleEmbeddedResource(context: context, filename: "setup.js", contentType: "application/javascript");
                break;

            case "/favicon.ico":
                await HandleEmbeddedBinary(context: context, filename: "favicon.ico", contentType: "image/x-icon");
                break;

            case "/setup/config":
                await HandleSetupConfig(context: context);
                break;

            case "/setup/status":
                await HandleSetupStatus(context: context);
                break;

            case "/setup/silent-sso":
                await HandleSilentSso(context: context);
                break;

            case "/setup/exchange":
                await HandleExchange(context: context);
                break;

            case "/setup/device-code":
                await HandleDeviceCode(context: context);
                break;

            case "/setup/retry":
                await HandleRetry(context: context);
                break;

            case "/setup/qr":
                await HandleQrCode(context: context);
                break;

            case "/sso-callback":
                await HandleSsoCallback(context: context);
                break;

            default:
                await HandleServiceUnavailable(context: context);
                break;
        }
    }

    // ── Static file handlers ────────────────────────────────────────────────

    private static async Task HandleSetupPage(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        string html = await LoadEmbeddedResource(filename: "setup.html");

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(text: html, encoding: Encoding.UTF8);
    }

    private static async Task HandleEmbeddedResource(
        HttpContext context,
        string filename,
        string contentType
    )
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        string content = await LoadEmbeddedResource(filename: filename);

        context.Response.ContentType = $"{contentType}; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(text: content, encoding: Encoding.UTF8);
    }

    // ── /setup/config ───────────────────────────────────────────────────────

    private async Task HandleSetupConfig(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        string codeChallenge;
        string pkceState;
        lock (_pkceLock)
        {
            codeChallenge = _codeChallenge;
            pkceState = _pkceState;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status200OK;

        object response = new
        {
            status = "setup_required",
            phase = _state.CurrentPhase.ToString(),
            error = _state.ErrorMessage,
            server_port = RuntimeServerSettings.Current.InternalServerPort,
            auth_base_url = ExternalServicesConfig.Current.AuthBaseUrl.OrEmpty(),
            client_id = ExternalServicesConfig.Current.TokenClientId.OrEmpty(),
            code_challenge = codeChallenge,
            pkce_state = pkceState,
            is_first_boot = !Start.Certificate!.HasValidCertificate(),
        };

        await WriteJsonResponse(response: context.Response, data: response);
    }

    // ── /setup/status ───────────────────────────────────────────────────────

    private async Task HandleSetupStatus(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        string accept = context.Request.Headers.Accept.ToString();

        if (accept.Contains(value: "text/event-stream"))
        {
            await HandleSetupStatusSse(context: context);
            return;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await WriteJsonResponse(response: context.Response, data: BuildStatusSnapshot());
    }

    private async Task HandleSetupStatusSse(HttpContext context)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.StatusCode = StatusCodes.Status200OK;

        CancellationToken cancellationToken = context.RequestAborted;

        string json = JsonConvert.SerializeObject(value: BuildStatusSnapshot());
        await WriteSseEvent(response: context.Response, data: json, cancellationToken: cancellationToken);

        while (!cancellationToken.IsCancellationRequested && _state.IsSetupRequired)
        {
            try
            {
                await _state.WaitForChangeAsync(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            json = JsonConvert.SerializeObject(value: BuildStatusSnapshot());
            await WriteSseEvent(response: context.Response, data: json, cancellationToken: cancellationToken);
        }
    }

    private object BuildStatusSnapshot()
    {
        return new
        {
            phase = _state.CurrentPhase.ToString(),
            is_setup_required = _state.IsSetupRequired,
            is_authenticated = _state.IsAuthenticated,
            error = _state.ErrorMessage,
            detail = _state.PhaseDetail,
            server_url = _state.ServerUrl,
        };
    }

    private static async Task WriteSseEvent(
        HttpResponse response,
        string data,
        CancellationToken cancellationToken
    )
    {
        await response.WriteAsync(text: $"data: {data}\n\n", encoding: Encoding.UTF8, cancellationToken: cancellationToken);
        await response.Body.FlushAsync(cancellationToken: cancellationToken);
    }

    // ── /setup/silent-sso ───────────────────────────────────────────────────

    private static async Task HandleSilentSso(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(
            text: "<html><body><script>parent.postMessage(location.href, location.origin)</script></body></html>",
            encoding: Encoding.UTF8
        );
    }

    // ── POST /setup/exchange ─────────────────────────────────────────────────

    private async Task HandleExchange(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Post)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        context.Response.ContentType = "application/json";

        ExchangeRequest? body;
        try
        {
            string rawBody = await new StreamReader(stream: context.Request.Body).ReadToEndAsync();
            body = rawBody.FromJson<ExchangeRequest>();
        }
        catch
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteJsonResponse(
                response: context.Response,
                data: new { status = "error", message = "Invalid request body" }
            );
            return;
        }

        if (body is null || string.IsNullOrEmpty(value: body.Code))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteJsonResponse(
                response: context.Response,
                data: new { status = "error", message = "Missing code" }
            );
            return;
        }

        string currentPkceState;
        string currentCodeVerifier;
        bool alreadyCompleted;

        lock (_pkceLock)
        {
            currentPkceState = _pkceState;
            currentCodeVerifier = _codeVerifier;
            alreadyCompleted = _exchangeCompleted;
        }

        if (!string.IsNullOrEmpty(value: body.State) && body.State != currentPkceState)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteJsonResponse(
                response: context.Response,
                data: new { status = "error", message = "Invalid state parameter" }
            );
            return;
        }

        if (alreadyCompleted)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await WriteJsonResponse(
                response: context.Response,
                data: new { status = "error", message = "Exchange already completed" }
            );
            return;
        }

        string redirectUri =
            $"{context.Request.Scheme}://{context.Request.Host.Value}/setup/silent-sso";

        try
        {
            if (string.IsNullOrEmpty(value: ExternalServicesConfig.Current.TokenClientId))
                throw new InvalidOperationException(message: "Auth configuration not available");

            List<KeyValuePair<string, string>> tokenBody = AuthManager.BuildAuthorizationCodeBody(
                clientId: ExternalServicesConfig.Current.TokenClientId,
                code: body.Code,
                redirectUri: redirectUri,
                codeVerifier: currentCodeVerifier
            );

            string tokenEndpoint =
                $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/token";

            using SystemHttpClient httpClient = new();
            httpClient.WithNoMercyUserAgent();

            using HttpResponseMessage tokenResponse = await httpClient.PostAsync(
                requestUri: tokenEndpoint,
                content: new FormUrlEncodedContent(nameValueCollection: tokenBody)
            );

            string responseContent = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    message: $"Token exchange failed ({(int)tokenResponse.StatusCode}): {responseContent}"
                );

            AuthResponse tokens =
                responseContent.FromJson<AuthResponse>()
                ?? throw new InvalidOperationException(message: "Failed to deserialize token response");

            await _authManager.StoreTokensAsync(tokens: tokens);
            _state.TransitionTo(targetPhase: SetupPhase.Authenticating);
            _state.TransitionTo(targetPhase: SetupPhase.Authenticated);

            lock (_pkceLock)
            {
                _exchangeCompleted = true;
            }

            RegeneratePkce();

            context.Response.StatusCode = StatusCodes.Status200OK;
            await WriteJsonResponse(response: context.Response, data: new { status = "ok" });
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"Silent SSO exchange failed: {ex.GetType().Name} — {ex.Message}",
                level: LogEventLevel.Warning
            );
            // Return a non-2xx so the client's fetch treats this as a failure — a 200
            // with an error body was silently swallowed and read as success.
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteJsonResponse(
                response: context.Response,
                data: new { status = "error", message = ex.Message }
            );
        }
    }

    // ── /sso-callback ───────────────────────────────────────────────────────

    private async Task HandleSsoCallback(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        string error = context.Request.Query[key: "error"].ToString();
        if (!string.IsNullOrEmpty(value: error))
        {
            string errorDescription = context.Request.Query[key: "error_description"].ToString();

            string displayMessage = !string.IsNullOrEmpty(value: errorDescription)
                ? $"Authorization failed: {errorDescription}"
                : $"Authorization failed: {error}";

            Logger.Setup(
                message: $"SSO callback error: {error} — {errorDescription}",
                level: LogEventLevel.Warning
            );

            _state.SetError(message: displayMessage);

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(
                text: BuildCallbackHtml(title: "Authorization Failed", message: displayMessage, isError: true),
                encoding: Encoding.UTF8
            );
            return;
        }

        string code = context.Request.Query[key: "code"].ToString();
        if (string.IsNullOrEmpty(value: code))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await WriteJsonResponse(
                response: context.Response,
                data: new { status = "error", message = "Missing authorization code" }
            );
            return;
        }

        string stateParam = context.Request.Query[key: "state"].ToString();
        string currentPkceState;
        string currentCodeVerifier;

        lock (_pkceLock)
        {
            currentPkceState = _pkceState;
            currentCodeVerifier = _codeVerifier;
        }

        if (!string.IsNullOrEmpty(value: stateParam) && stateParam != currentPkceState)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await WriteJsonResponse(
                response: context.Response,
                data: new { status = "error", message = "Invalid state parameter" }
            );
            _state.SetError(message: "Invalid state parameter during PKCE callback.");
            return;
        }

        _state.TransitionTo(targetPhase: SetupPhase.Authenticating);

        // Must byte-for-byte match the redirect_uri the browser sent to Keycloak.
        // A hardcoded localhost fails with invalid_grant when the user reaches setup
        // from a LAN IP or NAS hostname — derive it from the actual request host, the
        // same way HandleExchange does.
        string redirectUri =
            $"{context.Request.Scheme}://{context.Request.Host.Value}/sso-callback";
        string responseTitle;
        string responseMessage;
        bool responseIsError;

        try
        {
            if (string.IsNullOrEmpty(value: ExternalServicesConfig.Current.TokenClientId))
                throw new InvalidOperationException(message: "Auth configuration not available");

            List<KeyValuePair<string, string>> tokenBody = AuthManager.BuildAuthorizationCodeBody(
                clientId: ExternalServicesConfig.Current.TokenClientId,
                code: code,
                redirectUri: redirectUri,
                codeVerifier: currentCodeVerifier
            );

            string tokenEndpoint =
                $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/token";

            using SystemHttpClient httpClient = new();
            httpClient.WithNoMercyUserAgent();

            using HttpResponseMessage tokenResponse = await httpClient.PostAsync(
                requestUri: tokenEndpoint,
                content: new FormUrlEncodedContent(nameValueCollection: tokenBody)
            );

            string responseContent = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    message: $"Token exchange failed ({(int)tokenResponse.StatusCode}): {responseContent}"
                );

            AuthResponse tokens =
                responseContent.FromJson<AuthResponse>()
                ?? throw new InvalidOperationException(message: "Failed to deserialize token response");

            await _authManager.StoreTokensAsync(tokens: tokens);
            _state.TransitionTo(targetPhase: SetupPhase.Authenticating);
            _state.TransitionTo(targetPhase: SetupPhase.Authenticated);

            _terminalUi?.ShowProgress(phase: "Authenticated", detail: "Signed in via browser");
            Logger.Setup(message: "OAuth token exchange completed successfully");

            responseTitle = "Authentication Successful";
            responseMessage = "Tokens saved. Completing server registration...";
            responseIsError = false;
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"Token exchange failed: {ex.GetType().Name} — {ex.Message}",
                level: LogEventLevel.Error
            );
            // Transition first: TransitionTo clears ErrorMessage, so setting the error
            // before it wiped the message the /setup/status poll surfaces to the user.
            _state.TransitionTo(targetPhase: SetupPhase.Unauthenticated);
            _state.SetError(message: $"Sign in failed: {ex.Message}");

            responseTitle = "Authentication Failed";
            responseMessage = $"Sign in failed: {ex.Message}";
            responseIsError = true;

            RegeneratePkce();
        }

        if (!responseIsError)
        {
            RegeneratePkce();

            context.Response.Headers.Location = "/setup";
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(
                text: BuildCallbackHtml(title: responseTitle, message: responseMessage, isError: responseIsError),
                encoding: Encoding.UTF8
            );
            await context.Response.CompleteAsync();

            _ = Task.Run(function: async () =>
            {
                try
                {
                    await RunPostAuthRegistration();
                }
                catch (Exception ex)
                {
                    Logger.Setup(
                        message: $"Post-auth registration failed: {ex.GetType().Name} — {ex.Message}",
                        level: LogEventLevel.Error
                    );
                    _state.SetError(message: "Could not connect your server. Please try again.");
                }
            });
        }
        else
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(
                text: BuildCallbackHtml(title: responseTitle, message: responseMessage, isError: responseIsError),
                encoding: Encoding.UTF8
            );
        }
    }

    // ── POST /setup/device-code ─────────────────────────────────────────────

    private async Task HandleDeviceCode(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Post)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        context.Response.ContentType = "application/json";

        if (string.IsNullOrEmpty(value: ExternalServicesConfig.Current.TokenClientId))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await WriteJsonResponse(
                response: context.Response,
                data: new { error = true, message = "Auth configuration not available" }
            );
            return;
        }

        try
        {
            List<KeyValuePair<string, string>> deviceCodeBody =
                AuthManager.BuildDeviceCodeRequestBody(
                    clientId: ExternalServicesConfig.Current.TokenClientId
                );

            string deviceCodeEndpoint =
                $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/auth/device";

            using SystemHttpClient httpClient = new();
            httpClient.WithNoMercyUserAgent();

            using HttpResponseMessage deviceResponse = await httpClient.PostAsync(
                requestUri: deviceCodeEndpoint,
                content: new FormUrlEncodedContent(nameValueCollection: deviceCodeBody)
            );

            string deviceCodeResponse = await deviceResponse.Content.ReadAsStringAsync();

            // A Keycloak 4xx returns an error body that still deserializes into a
            // DeviceAuthResponse with empty fields — check the status first so a failed
            // request isn't handed to the user as an empty-but-valid device code.
            if (!deviceResponse.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    message: $"Device code request failed ({(int)deviceResponse.StatusCode}): {deviceCodeResponse}"
                );

            DeviceAuthResponse deviceData =
                deviceCodeResponse.FromJson<DeviceAuthResponse>()
                ?? throw new InvalidOperationException(message: "Failed to get device code");

            context.Response.StatusCode = StatusCodes.Status200OK;
            await WriteJsonResponse(
                response: context.Response,
                data: new
                {
                    user_code = deviceData.UserCode,
                    verification_uri = deviceData.VerificationUri,
                    verification_uri_complete = deviceData.VerificationUriComplete,
                    expires_in = deviceData.ExpiresIn,
                    interval = deviceData.Interval,
                }
            );

            if (SetupTerminalUi.IsInteractiveTerminal)
            {
                string setupPageUrl =
                    $"http://localhost:{RuntimeServerSettings.Current.InternalServerPort}/setup";
                SetupTerminalUi terminalUi = _terminalUi ?? new SetupTerminalUi();
                terminalUi.Show(
                    verificationUriComplete: deviceData.VerificationUriComplete,
                    verificationUri: deviceData.VerificationUri,
                    userCode: deviceData.UserCode,
                    setupPageUrl: setupPageUrl
                );
            }

            _ = Task.Run(function: async () =>
            {
                await PollDeviceGrant(deviceData: deviceData);
            });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await WriteJsonResponse(
                response: context.Response,
                data: new { error = true, message = $"Failed to initiate device login: {ex.Message}" }
            );
        }
    }

    // ── POST /setup/retry ───────────────────────────────────────────────────

    private async Task HandleRetry(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Post)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        context.Response.ContentType = "application/json";

        SetupPhase phase = _state.CurrentPhase;

        if (phase == SetupPhase.Authenticated || phase == SetupPhase.Registering)
        {
            _state.ClearError();

            context.Response.StatusCode = StatusCodes.Status200OK;
            await WriteJsonResponse(response: context.Response, data: new { status = "retrying" });

            _ = Task.Run(function: async () =>
            {
                try
                {
                    _state.TransitionTo(targetPhase: SetupPhase.Authenticated);
                    await RunPostAuthRegistration();
                }
                catch (Exception ex)
                {
                    Logger.Setup(
                        message: $"Registration retry failed: {ex.GetType().Name} — {ex.Message}",
                        level: LogEventLevel.Error
                    );
                    _state.SetError(
                        message: "Could not connect your server. Check your internet connection and try again."
                    );
                }
            });

            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        await WriteJsonResponse(response: context.Response, data: new { status = "unauthenticated" });
    }

    // ── /setup/qr ───────────────────────────────────────────────────────────

    private static async Task HandleQrCode(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        string url = context.Request.Query[key: "data"].ToString();

        if (string.IsNullOrEmpty(value: url))
            url = context.Request.Query[key: "url"].ToString();

        if (string.IsNullOrEmpty(value: url))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        byte[] pngBytes = GenerateQrPng(url: url);

        context.Response.ContentType = "image/png";
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentLength = pngBytes.Length;
        await context.Response.Body.WriteAsync(buffer: pngBytes);
    }

    // ── 503 catch-all ───────────────────────────────────────────────────────

    private static async Task HandleServiceUnavailable(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        await WriteJsonResponse(
            response: context.Response,
            data: new
            {
                status = "setup_required",
                message = "Server is in setup mode",
                setup_url = "/setup",
            }
        );
    }

    // ── Post-auth registration ───────────────────────────────────────────────

    private async Task RunPostAuthRegistration()
    {
        if (_state.CurrentPhase != SetupPhase.Authenticated)
            return;

        try
        {
            using CancellationTokenSource dbTimeoutCts = new(delay: TimeSpan.FromSeconds(seconds: 30));
            bool dbReady = await CronWorker.GetDatabaseReadyTask().WaitAsync(cancellationToken: dbTimeoutCts.Token);
            if (!dbReady)
                Logger.Setup(
                    message: "Database schema init reported failure — continuing registration anyway",
                    level: LogEventLevel.Warning
                );

            _state.TransitionTo(targetPhase: SetupPhase.Registering);
            _terminalUi?.ShowProgress(phase: "Registering", detail: "Connecting your server to NoMercy...");

            if (Start.NetworkDiscovery is not null)
                await Start.NetworkDiscovery.DiscoverExternalIpAsync();
            using CancellationTokenSource registrationTimeoutCts = new(delay: TimeSpan.FromMinutes(minutes: 2));
            await _serverRegistrationService.Init().WaitAsync(cancellationToken: registrationTimeoutCts.Token);

            _state.TransitionTo(targetPhase: SetupPhase.Registered);
            _terminalUi?.ShowProgress(phase: "Registered", detail: "Setting up your server address...");

            _state.SetPhaseDetail(
                detail: "Securing your connection... (this can take a couple of minutes)"
            );
            _terminalUi?.SetStatus(message: "Securing your connection...");

            if (Start.Certificate!.HasValidCertificate())
            {
                string serverUrl =
                    $"https://{Info.DeviceId}.nomercy.tv:{RuntimeServerSettings.Current.ExternalServerPort}";
                _state.SetServerUrl(url: serverUrl);
                _state.TransitionTo(targetPhase: SetupPhase.CertificateAcquired);
                _state.TransitionTo(targetPhase: SetupPhase.Complete);
                _terminalUi?.ShowComplete(serverUrl: serverUrl);
                Logger.Setup(message: "Setup complete — server will restart with HTTPS");
            }
            else
            {
                _state.SetError(message: "Registration completed but certificate was not acquired");
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Setup(
                message: "Post-auth registration timed out (registration_timeout)",
                level: LogEventLevel.Error
            );
            _state.SetError(message: "Registration timed out. Please check your connection and try again.");
            _state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"Post-auth registration failed: {ex.GetType().Name} — {ex.Message}",
                level: LogEventLevel.Error
            );
            _state.SetError(message: "Could not connect your server. Please try again.");
            _state.TransitionTo(targetPhase: SetupPhase.Authenticated);
        }
    }

    // ── Device grant polling ────────────────────────────────────────────────

    private async Task PollDeviceGrant(DeviceAuthResponse deviceData)
    {
        if (string.IsNullOrEmpty(value: ExternalServicesConfig.Current.TokenClientId))
            return;

        // Don't transition to Authenticating here — that hides the login UI.
        // Transition only happens when the user completes auth (success path below).

        // If auth already completed (e.g. via browser login while device poll runs), stop.
        if (_state.IsAuthenticated || _state.CurrentPhase == SetupPhase.Complete)
            return;

        List<KeyValuePair<string, string>> tokenBody = AuthManager.BuildDeviceTokenBody(
            clientId: ExternalServicesConfig.Current.TokenClientId,
            deviceCode: deviceData.DeviceCode
        );

        string tokenEndpoint =
            $"{ExternalServicesConfig.Current.AuthBaseUrl}protocol/openid-connect/token";
        DateTime expiresAt = DateTime.UtcNow.AddSeconds(value: deviceData.ExpiresIn);

        using SystemHttpClient httpClient = new();
        httpClient.WithNoMercyUserAgent();

        // Clamp the server-supplied interval so a hostile or buggy IDP can't
        // tight-loop us (interval=0) or stall the setup phase (huge value).
        int intervalSec = Math.Clamp(value: deviceData.Interval, min: 1, max: 30);

        while (DateTime.UtcNow < expiresAt)
        {
            await Task.Delay(millisecondsDelay: intervalSec * 1000);

            // Stop polling if auth completed via another path (browser login, silent SSO)
            if (_state.IsAuthenticated || _state.CurrentPhase == SetupPhase.Complete)
                return;

            try
            {
                using HttpResponseMessage response = await httpClient.PostAsync(
                    requestUri: tokenEndpoint,
                    content: new FormUrlEncodedContent(nameValueCollection: tokenBody)
                );

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    AuthResponse data =
                        content.FromJson<AuthResponse>()
                        ?? throw new InvalidOperationException(
                            message: "Failed to deserialize token response"
                        );

                    await _authManager.StoreTokensAsync(tokens: data);
                    _state.TransitionTo(targetPhase: SetupPhase.Authenticating);
                    _state.TransitionTo(targetPhase: SetupPhase.Authenticated);

                    _terminalUi?.ShowProgress(phase: "Authenticated", detail: "Signed in successfully!");

                    await RunPostAuthRegistration();
                    return;
                }

                string errorContent = await response.Content.ReadAsStringAsync();
                dynamic? error = JsonConvert.DeserializeObject<dynamic>(value: errorContent);
                string? errorCode = error?.error?.ToString();

                // RFC 8628 §3.5: slow_down means keep polling but back off — it is NOT
                // fatal. Treating it as fatal aborted an otherwise-recoverable device login.
                if (errorCode == "slow_down")
                {
                    intervalSec = Math.Clamp(value: intervalSec + 5, min: 1, max: 30);
                    continue;
                }

                if (errorCode != "authorization_pending")
                {
                    // Transition first: TransitionTo clears ErrorMessage.
                    _state.TransitionTo(targetPhase: SetupPhase.Unauthenticated);
                    _state.SetError(message: $"Device login failed: {error?.error_description}");
                    return;
                }
            }
            catch (Exception ex)
            {
                _state.TransitionTo(targetPhase: SetupPhase.Unauthenticated);
                _state.SetError(message: $"Device login error: {ex.Message}");
                return;
            }
        }

        _state.TransitionTo(targetPhase: SetupPhase.Unauthenticated);
        _state.SetError(message: "Device authorization timed out");
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void RegeneratePkce()
    {
        lock (_pkceLock)
        {
            _codeVerifier = AuthManager.GenerateCodeVerifier();
            _codeChallenge = AuthManager.GenerateCodeChallenge(codeVerifier: _codeVerifier);
            _pkceState = Guid.NewGuid().ToString(format: "N");
            _exchangeCompleted = false;
        }
    }

    private static async Task<string> LoadEmbeddedResource(string filename)
    {
        Assembly assembly = typeof(SetupEndpoints).Assembly;
        string resourceName = $"NoMercy.Setup.Resources.{filename}";
        await using Stream? stream = assembly.GetManifestResourceStream(name: resourceName);
        if (stream is null)
            throw new FileNotFoundException(message: $"Embedded resource not found: {resourceName}");
        using StreamReader reader = new(stream: stream);
        return await reader.ReadToEndAsync();
    }

    internal static string BuildCallbackHtml(string title, string message, bool isError = false)
    {
        string color = isError ? "#f08080" : "#CBAFFF";

        if (isError)
        {
            return "<!DOCTYPE html><html><head>"
                + "<meta charset=\"UTF-8\">"
                + "<style>"
                + "body{background:#0a0a0f;color:#e0e0e0;font-family:-apple-system,"
                + "BlinkMacSystemFont,\"Segoe UI\",Roboto,sans-serif;"
                + "display:flex;align-items:center;justify-content:center;"
                + "min-height:100vh;margin:0;}"
                + ".card{background:#16161e;border:1px solid #2a2a3a;"
                + "border-radius:12px;padding:32px 24px;text-align:center;"
                + "max-width:440px;width:100%;}"
                + $"h2{{color:{color};margin-bottom:12px;}}"
                + "p{color:#999;font-size:14px;}"
                + "</style></head><body>"
                + "<div class=\"card\">"
                + $"<h2>{WebUtility.HtmlEncode(value: title)}</h2>"
                + $"<p>{WebUtility.HtmlEncode(value: message)}</p>"
                + "<p style=\"margin-top:16px;color:#666;\">Redirecting to setup...</p>"
                + "</div>"
                + "<script>setTimeout(function(){window.location.href='/setup';}, 1500);</script>"
                + "</body></html>";
        }

        // Success case: redirect back to setup page to show progress
        return "<!DOCTYPE html><html><head>"
            + "<meta charset=\"UTF-8\">"
            + "<style>"
            + "body{background:#0a0a0f;color:#e0e0e0;font-family:-apple-system,"
            + "BlinkMacSystemFont,\"Segoe UI\",Roboto,sans-serif;"
            + "display:flex;align-items:center;justify-content:center;"
            + "min-height:100vh;margin:0;}"
            + ".card{background:#16161e;border:1px solid #2a2a3a;"
            + "border-radius:12px;padding:32px 24px;text-align:center;"
            + "max-width:440px;width:100%;}"
            + $"h2{{color:{color};margin-bottom:12px;}}"
            + "p{color:#999;font-size:14px;}"
            + "</style></head><body>"
            + "<div class=\"card\">"
            + $"<h2>{WebUtility.HtmlEncode(value: title)}</h2>"
            + $"<p>{WebUtility.HtmlEncode(value: message)}</p>"
            + "<p style=\"margin-top:16px;color:#666;\">Redirecting to setup...</p>"
            + "</div>"
            + "<script>setTimeout(function(){window.location.href='/setup';}, 1500);</script>"
            + "</body></html>";
    }

    internal static string BuildRedirectUri(HttpRequest request)
    {
        int port = request.Host.Port ?? RuntimeServerSettings.Current.InternalServerPort;
        return $"http://localhost:{port}/sso-callback";
    }

    private static async Task HandleEmbeddedBinary(
        HttpContext context,
        string filename,
        string contentType
    )
    {
        Assembly assembly = typeof(SetupEndpoints).Assembly;
        string resourceName = $"NoMercy.Setup.Resources.{filename}";
        await using Stream? stream = assembly.GetManifestResourceStream(name: resourceName);
        if (stream is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = contentType;
        context.Response.StatusCode = StatusCodes.Status200OK;
        await stream.CopyToAsync(destination: context.Response.Body);
    }

    private static byte[] GenerateQrPng(string url)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData qrData = generator.CreateQrCode(plainText: url, eccLevel: QRCodeGenerator.ECCLevel.L);
        using PngByteQRCode qrCode = new(data: qrData);
        return qrCode.GetGraphic(pixelsPerModule: 8);
    }

    private static async Task WriteJsonResponse(HttpResponse response, object data)
    {
        string json = JsonConvert.SerializeObject(value: data, formatting: Formatting.Indented);
        await response.WriteAsync(text: json, encoding: Encoding.UTF8);
    }

    // ── Inner types ──────────────────────────────────────────────────────────

    private sealed class ExchangeRequest
    {
        [JsonProperty(propertyName: "code")]
        public string? Code { get; set; }

        [JsonProperty(propertyName: "state")]
        public string? State { get; set; }
    }
}
