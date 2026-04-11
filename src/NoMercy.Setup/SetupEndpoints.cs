using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NoMercy.Networking;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercyQueue.Workers;
using QRCoder;
using Serilog.Events;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Setup;

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

    public SetupEndpoints(SetupState state, AuthManager authManager)
    {
        _state = state;
        _authManager = authManager;
        _terminalUi = SetupTerminalUi.IsInteractiveTerminal ? new SetupTerminalUi() : null;

        _codeVerifier = AuthManager.GenerateCodeVerifier();
        _codeChallenge = AuthManager.GenerateCodeChallenge(_codeVerifier);
        _pkceState = Guid.NewGuid().ToString("N");
        _exchangeCompleted = false;
    }

    // ── Dispatcher ──────────────────────────────────────────────────────────

    public async Task HandleRequestAsync(HttpContext context)
    {
        string path = (context.Request.Path.Value?.TrimEnd('/') ?? string.Empty).ToLowerInvariant();

        switch (path)
        {
            case "/setup":
                await HandleSetupPage(context);
                break;

            case "/setup/setup.css":
                await HandleEmbeddedResource(context, "setup.css", "text/css");
                break;

            case "/setup/setup.js":
                await HandleEmbeddedResource(context, "setup.js", "application/javascript");
                break;

            case "/favicon.ico":
                await HandleEmbeddedBinary(context, "favicon.ico", "image/x-icon");
                break;

            case "/setup/config":
                await HandleSetupConfig(context);
                break;

            case "/setup/status":
                await HandleSetupStatus(context);
                break;

            case "/setup/silent-sso":
                await HandleSilentSso(context);
                break;

            case "/setup/exchange":
                await HandleExchange(context);
                break;

            case "/setup/device-code":
                await HandleDeviceCode(context);
                break;

            case "/setup/retry":
                await HandleRetry(context);
                break;

            case "/setup/qr":
                await HandleQrCode(context);
                break;

            case "/sso-callback":
                await HandleSsoCallback(context);
                break;

            default:
                await HandleServiceUnavailable(context);
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

        string html = await LoadEmbeddedResource("setup.html");

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(html, Encoding.UTF8);
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

        string content = await LoadEmbeddedResource(filename);

        context.Response.ContentType = $"{contentType}; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(content, Encoding.UTF8);
    }

    // ── /setup/config ───────────────────────────────────────────────────────

    private async Task HandleSetupConfig(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        RegeneratePkce();

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
            server_port = Config.InternalServerPort,
            auth_base_url = Config.AuthBaseUrl.OrEmpty(),
            client_id = Config.TokenClientId.OrEmpty(),
            code_challenge = codeChallenge,
            pkce_state = pkceState,
            is_first_boot = !Register.IsRegistered,
        };

        await WriteJsonResponse(context.Response, response);
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

        if (accept.Contains("text/event-stream"))
        {
            await HandleSetupStatusSse(context);
            return;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await WriteJsonResponse(context.Response, BuildStatusSnapshot());
    }

    private async Task HandleSetupStatusSse(HttpContext context)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.StatusCode = StatusCodes.Status200OK;

        CancellationToken cancellationToken = context.RequestAborted;

        string json = JsonConvert.SerializeObject(BuildStatusSnapshot());
        await WriteSseEvent(context.Response, json, cancellationToken);

        while (!cancellationToken.IsCancellationRequested && _state.IsSetupRequired)
        {
            try
            {
                await _state.WaitForChangeAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            json = JsonConvert.SerializeObject(BuildStatusSnapshot());
            await WriteSseEvent(context.Response, json, cancellationToken);
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
        await response.WriteAsync($"data: {data}\n\n", Encoding.UTF8, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
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
            "<html><body><script>parent.postMessage(location.href, location.origin)</script></body></html>",
            Encoding.UTF8
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
            string rawBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            body = rawBody.FromJson<ExchangeRequest>();
        }
        catch
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteJsonResponse(
                context.Response,
                new { status = "error", message = "Invalid request body" }
            );
            return;
        }

        if (body is null || string.IsNullOrEmpty(body.Code))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteJsonResponse(
                context.Response,
                new { status = "error", message = "Missing code" }
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

        if (!string.IsNullOrEmpty(body.State) && body.State != currentPkceState)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteJsonResponse(
                context.Response,
                new { status = "error", message = "Invalid state parameter" }
            );
            return;
        }

        if (alreadyCompleted)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await WriteJsonResponse(
                context.Response,
                new { status = "error", message = "Exchange already completed" }
            );
            return;
        }

        string redirectUri = $"http://localhost:{Config.InternalServerPort}/setup/silent-sso";

        try
        {
            if (string.IsNullOrEmpty(Config.TokenClientId))
                throw new InvalidOperationException("Auth configuration not available");

            List<KeyValuePair<string, string>> tokenBody = AuthManager.BuildAuthorizationCodeBody(
                Config.TokenClientId,
                body.Code,
                redirectUri,
                currentCodeVerifier
            );

            string tokenEndpoint = $"{Config.AuthBaseUrl}protocol/openid-connect/token";

            using SystemHttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);

            using HttpResponseMessage tokenResponse = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(tokenBody)
            );

            string responseContent = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Token exchange failed ({(int)tokenResponse.StatusCode}): {responseContent}"
                );

            Dto.AuthResponse tokens =
                responseContent.FromJson<Dto.AuthResponse>()
                ?? throw new InvalidOperationException("Failed to deserialize token response");

            await _authManager.StoreTokensAsync(tokens);
            _state.TransitionTo(SetupPhase.Authenticating);
            _state.TransitionTo(SetupPhase.Authenticated);

            lock (_pkceLock)
            {
                _exchangeCompleted = true;
            }

            RegeneratePkce();

            context.Response.StatusCode = StatusCodes.Status200OK;
            await WriteJsonResponse(context.Response, new { status = "ok" });
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"Silent SSO exchange failed: {ex.GetType().Name} — {ex.Message}",
                LogEventLevel.Warning
            );
            context.Response.StatusCode = StatusCodes.Status200OK;
            await WriteJsonResponse(
                context.Response,
                new { status = "error", message = ex.Message }
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

        string error = context.Request.Query["error"].ToString();
        if (!string.IsNullOrEmpty(error))
        {
            string errorDescription = context.Request.Query["error_description"].ToString();

            string displayMessage = !string.IsNullOrEmpty(errorDescription)
                ? $"Authorization failed: {errorDescription}"
                : $"Authorization failed: {error}";

            Logger.Setup(
                $"SSO callback error: {error} — {errorDescription}",
                LogEventLevel.Warning
            );

            _state.SetError(displayMessage);

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(
                SetupServer.BuildCallbackHtml(
                    "Authorization Failed",
                    displayMessage,
                    isError: true
                ),
                Encoding.UTF8
            );
            return;
        }

        string code = context.Request.Query["code"].ToString();
        if (string.IsNullOrEmpty(code))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await WriteJsonResponse(
                context.Response,
                new { status = "error", message = "Missing authorization code" }
            );
            return;
        }

        string stateParam = context.Request.Query["state"].ToString();
        string currentPkceState;
        string currentCodeVerifier;

        lock (_pkceLock)
        {
            currentPkceState = _pkceState;
            currentCodeVerifier = _codeVerifier;
        }

        if (!string.IsNullOrEmpty(stateParam) && stateParam != currentPkceState)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await WriteJsonResponse(
                context.Response,
                new { status = "error", message = "Invalid state parameter" }
            );
            _state.SetError("Invalid state parameter during PKCE callback.");
            return;
        }

        _state.TransitionTo(SetupPhase.Authenticating);

        string redirectUri = $"http://localhost:{Config.InternalServerPort}/sso-callback";
        string responseTitle;
        string responseMessage;
        bool responseIsError;

        try
        {
            if (string.IsNullOrEmpty(Config.TokenClientId))
                throw new InvalidOperationException("Auth configuration not available");

            List<KeyValuePair<string, string>> tokenBody = AuthManager.BuildAuthorizationCodeBody(
                Config.TokenClientId,
                code,
                redirectUri,
                currentCodeVerifier
            );

            string tokenEndpoint = $"{Config.AuthBaseUrl}protocol/openid-connect/token";

            using SystemHttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);

            using HttpResponseMessage tokenResponse = await httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(tokenBody)
            );

            string responseContent = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Token exchange failed ({(int)tokenResponse.StatusCode}): {responseContent}"
                );

            Dto.AuthResponse tokens =
                responseContent.FromJson<Dto.AuthResponse>()
                ?? throw new InvalidOperationException("Failed to deserialize token response");

            await _authManager.StoreTokensAsync(tokens);
            _state.TransitionTo(SetupPhase.Authenticating);
            _state.TransitionTo(SetupPhase.Authenticated);

            _terminalUi?.ShowProgress("Authenticated", "Signed in via browser");
            Logger.Setup("OAuth token exchange completed successfully");

            responseTitle = "Authentication Successful";
            responseMessage = "Tokens saved. Completing server registration...";
            responseIsError = false;
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"Token exchange failed: {ex.GetType().Name} — {ex.Message}",
                LogEventLevel.Error
            );
            _state.SetError($"Sign in failed: {ex.Message}");
            _state.TransitionTo(SetupPhase.Unauthenticated);

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
                SetupServer.BuildCallbackHtml(responseTitle, responseMessage, responseIsError),
                Encoding.UTF8
            );
            await context.Response.CompleteAsync();

            _ = Task.Run(async () =>
            {
                try
                {
                    await RunPostAuthRegistration();
                }
                catch (Exception ex)
                {
                    Logger.Setup(
                        $"Post-auth registration failed: {ex.GetType().Name} — {ex.Message}",
                        LogEventLevel.Error
                    );
                    _state.SetError("Could not connect your server. Please try again.");
                }
            });
        }
        else
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(
                SetupServer.BuildCallbackHtml(responseTitle, responseMessage, responseIsError),
                Encoding.UTF8
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

        if (string.IsNullOrEmpty(Config.TokenClientId))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await WriteJsonResponse(
                context.Response,
                new { error = true, message = "Auth configuration not available" }
            );
            return;
        }

        try
        {
            List<KeyValuePair<string, string>> deviceCodeBody =
                AuthManager.BuildDeviceCodeRequestBody(Config.TokenClientId);

            string deviceCodeEndpoint = $"{Config.AuthBaseUrl}protocol/openid-connect/auth/device";

            using SystemHttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);

            using HttpResponseMessage deviceResponse = await httpClient.PostAsync(
                deviceCodeEndpoint,
                new FormUrlEncodedContent(deviceCodeBody)
            );

            string deviceCodeResponse = await deviceResponse.Content.ReadAsStringAsync();

            Dto.DeviceAuthResponse deviceData =
                deviceCodeResponse.FromJson<Dto.DeviceAuthResponse>()
                ?? throw new InvalidOperationException("Failed to get device code");

            context.Response.StatusCode = StatusCodes.Status200OK;
            await WriteJsonResponse(
                context.Response,
                new
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
                string setupPageUrl = $"http://localhost:{Config.InternalServerPort}/setup";
                SetupTerminalUi terminalUi = _terminalUi ?? new SetupTerminalUi();
                terminalUi.Show(
                    deviceData.VerificationUriComplete,
                    deviceData.VerificationUri,
                    deviceData.UserCode,
                    setupPageUrl
                );
            }

            _ = Task.Run(async () =>
            {
                await PollDeviceGrant(deviceData);
            });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await WriteJsonResponse(
                context.Response,
                new { error = true, message = $"Failed to initiate device login: {ex.Message}" }
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
            await WriteJsonResponse(context.Response, new { status = "retrying" });

            _ = Task.Run(async () =>
            {
                try
                {
                    _state.TransitionTo(SetupPhase.Authenticated);
                    await RunPostAuthRegistration();
                }
                catch (Exception ex)
                {
                    Logger.Setup(
                        $"Registration retry failed: {ex.GetType().Name} — {ex.Message}",
                        LogEventLevel.Error
                    );
                    _state.SetError(
                        "Could not connect your server. Check your internet connection and try again."
                    );
                }
            });

            return;
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
        await WriteJsonResponse(context.Response, new { status = "unauthenticated" });
    }

    // ── /setup/qr ───────────────────────────────────────────────────────────

    private static async Task HandleQrCode(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        string url = context.Request.Query["data"].ToString();

        if (string.IsNullOrEmpty(url))
            url = context.Request.Query["url"].ToString();

        if (string.IsNullOrEmpty(url))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        byte[] pngBytes = GenerateQrPng(url);

        context.Response.ContentType = "image/png";
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentLength = pngBytes.Length;
        await context.Response.Body.WriteAsync(pngBytes);
    }

    // ── 503 catch-all ───────────────────────────────────────────────────────

    private static async Task HandleServiceUnavailable(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        await WriteJsonResponse(
            context.Response,
            new
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
            using CancellationTokenSource dbTimeoutCts = new(TimeSpan.FromSeconds(30));
            bool dbReady = await CronWorker.GetDatabaseReadyTask().WaitAsync(dbTimeoutCts.Token);
            if (!dbReady)
                Logger.Setup(
                    "Database schema init reported failure — continuing registration anyway",
                    LogEventLevel.Warning
                );

            _state.TransitionTo(SetupPhase.Registering);
            _terminalUi?.ShowProgress("Registering", "Connecting your server to NoMercy...");

            if (Start.NetworkDiscovery is not null)
                await Start.NetworkDiscovery.DiscoverExternalIpAsync();
            await Register.Init();

            _state.TransitionTo(SetupPhase.Registered);
            _terminalUi?.ShowProgress("Registered", "Setting up your server address...");

            _state.SetPhaseDetail(
                "Securing your connection... (this can take a couple of minutes)"
            );
            _terminalUi?.SetStatus("Securing your connection...");

            if (Certificate.HasValidCertificate())
            {
                string serverUrl =
                    $"https://{Info.DeviceId}.nomercy.tv:{Config.ExternalServerPort}";
                _state.SetServerUrl(serverUrl);
                _state.TransitionTo(SetupPhase.CertificateAcquired);
                _state.TransitionTo(SetupPhase.Complete);
                _terminalUi?.ShowComplete(serverUrl);
                Logger.Setup("Setup complete — server will restart with HTTPS");
            }
            else
            {
                _state.SetError("Registration completed but certificate was not acquired");
            }
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"Post-auth registration failed: {ex.GetType().Name} — {ex.Message}",
                LogEventLevel.Error
            );
            _state.SetError("Could not connect your server. Please try again.");
            _state.TransitionTo(SetupPhase.Authenticated);
        }
    }

    // ── Device grant polling ────────────────────────────────────────────────

    private async Task PollDeviceGrant(Dto.DeviceAuthResponse deviceData)
    {
        if (string.IsNullOrEmpty(Config.TokenClientId))
            return;

        // Don't transition to Authenticating here — that hides the login UI.
        // Transition only happens when the user completes auth (success path below).

        // If auth already completed (e.g. via browser login while device poll runs), stop.
        if (_state.IsAuthenticated || _state.CurrentPhase == SetupPhase.Complete)
            return;

        List<KeyValuePair<string, string>> tokenBody = AuthManager.BuildDeviceTokenBody(
            Config.TokenClientId,
            deviceData.DeviceCode
        );

        string tokenEndpoint = $"{Config.AuthBaseUrl}protocol/openid-connect/token";
        DateTime expiresAt = DateTime.Now.AddSeconds(deviceData.ExpiresIn);

        using SystemHttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Config.UserAgent);

        while (DateTime.Now < expiresAt)
        {
            await Task.Delay(deviceData.Interval * 1000);

            // Stop polling if auth completed via another path (browser login, silent SSO)
            if (_state.IsAuthenticated || _state.CurrentPhase == SetupPhase.Complete)
                return;

            try
            {
                using HttpResponseMessage response = await httpClient.PostAsync(
                    tokenEndpoint,
                    new FormUrlEncodedContent(tokenBody)
                );

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    Dto.AuthResponse data =
                        content.FromJson<Dto.AuthResponse>()
                        ?? throw new InvalidOperationException(
                            "Failed to deserialize token response"
                        );

                    await _authManager.StoreTokensAsync(data);
                    _state.TransitionTo(SetupPhase.Authenticating);
                    _state.TransitionTo(SetupPhase.Authenticated);

                    _terminalUi?.ShowProgress("Authenticated", "Signed in successfully!");

                    await RunPostAuthRegistration();
                    return;
                }

                string errorContent = await response.Content.ReadAsStringAsync();
                dynamic? error = JsonConvert.DeserializeObject<dynamic>(errorContent);
                if (error?.error?.ToString() != "authorization_pending")
                {
                    _state.SetError($"Device login failed: {error?.error_description}");
                    _state.TransitionTo(SetupPhase.Unauthenticated);
                    return;
                }
            }
            catch (Exception ex)
            {
                _state.SetError($"Device login error: {ex.Message}");
                _state.TransitionTo(SetupPhase.Unauthenticated);
                return;
            }
        }

        _state.SetError("Device authorization timed out");
        _state.TransitionTo(SetupPhase.Unauthenticated);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void RegeneratePkce()
    {
        lock (_pkceLock)
        {
            _codeVerifier = AuthManager.GenerateCodeVerifier();
            _codeChallenge = AuthManager.GenerateCodeChallenge(_codeVerifier);
            _pkceState = Guid.NewGuid().ToString("N");
            _exchangeCompleted = false;
        }
    }

    private static async Task<string> LoadEmbeddedResource(string filename)
    {
        Assembly assembly = typeof(SetupEndpoints).Assembly;
        string resourceName = $"NoMercy.Setup.Resources.{filename}";
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        using StreamReader reader = new(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task HandleEmbeddedBinary(
        HttpContext context,
        string filename,
        string contentType
    )
    {
        Assembly assembly = typeof(SetupEndpoints).Assembly;
        string resourceName = $"NoMercy.Setup.Resources.{filename}";
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = contentType;
        context.Response.StatusCode = StatusCodes.Status200OK;
        await stream.CopyToAsync(context.Response.Body);
    }

    private static byte[] GenerateQrPng(string url)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData qrData = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.L);
        using PngByteQRCode qrCode = new(qrData);
        return qrCode.GetGraphic(8);
    }

    private static async Task WriteJsonResponse(HttpResponse response, object data)
    {
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        await response.WriteAsync(json, Encoding.UTF8);
    }

    // ── Inner types ──────────────────────────────────────────────────────────

    private sealed class ExchangeRequest
    {
        [Newtonsoft.Json.JsonProperty("code")]
        public string? Code { get; set; }

        [Newtonsoft.Json.JsonProperty("state")]
        public string? State { get; set; }
    }
}
