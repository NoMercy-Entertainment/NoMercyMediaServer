using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Newtonsoft.Json;
using NoMercy.Networking;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercyQueue.Workers;
using QRCoder;
using Serilog.Events;

namespace NoMercy.Setup;

public class SetupServer
{
    private readonly SetupState _state;
    private WebApplication? _host;
    private readonly int _port;

    private static string? _cachedSetupHtml;

    private string _codeVerifier;
    private string _codeChallenge;

    // Terminal UI — created lazily when device code flow starts
    private SetupTerminalUi? _terminalUi;

    public bool IsRunning { get; private set; }

    public SetupServer(SetupState state, int? port = null)
    {
        _state = state;
        _port = port ?? Config.InternalServerPort;
        _codeVerifier = Auth.GenerateCodeVerifier();
        _codeChallenge = Auth.GenerateCodeChallenge(_codeVerifier);
        
        string stateParam = Auth.GenerateCodeVerifier();
        Auth.SetState(stateParam);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(kestrelOptions =>
        {
            kestrelOptions.Listen(
                IPAddress.Any,
                _port,
                listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1;
                }
            );
        });

        _host = builder.Build();
        _host.UseRouting();
        _host.Run(HandleRequest);

        await _host.StartAsync(cancellationToken);
        IsRunning = true;

        Logger.Setup($"Setup server listening on http://0.0.0.0:{_port}");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning || _host == null)
            return;

        try
        {
            await _host.StopAsync(cancellationToken);
        }
        finally
        {
            await _host.DisposeAsync();
            _host = null;
            IsRunning = false;

            Logger.Setup("Setup server stopped");
        }
    }

    public async Task HandleRequest(HttpContext context)
    {
        string path = (context.Request.Path.Value?.TrimEnd('/')).OrEmpty();

        switch (path.ToLowerInvariant())
        {
            case "/setup":
                await HandleSetupPage(context);
                break;

            case "/setup/config":
                await HandleSetupConfig(context);
                break;

            case "/setup/status":
                await HandleSetupStatus(context);
                break;

            case "/setup/qr":
                await HandleQrCode(context);
                break;

            case "/setup/device-code":
                await HandleDeviceCode(context);
                break;

            case "/setup/retry":
                await HandleRetry(context);
                break;

            case "/sso-callback":
                await HandleSsoCallback(context);
                break;

            default:
                await HandleServiceUnavailable(context);
                break;
        }
    }

    private async Task HandleSetupPage(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        string html = LoadSetupHtml();

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(html, Encoding.UTF8);
    }

    private async Task HandleSetupConfig(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status200OK;

        object response = new
        {
            status = "setup_required",
            phase = _state.CurrentPhase.ToString(),
            error = _state.ErrorMessage,
            server_port = _port,
            auth_base_url = Config.AuthBaseUrl.OrEmpty(),
            client_id = Config.TokenClientId.OrEmpty(),
            code_challenge = _codeChallenge,
            state = Auth.GetState(),
        };

        await WriteJsonResponse(context.Response, response);
    }

    internal async Task HandleSsoCallback(HttpContext context)
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
                BuildCallbackHtml(
                    "Authorization Failed",
                    displayMessage,
                    isError: true
                )
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

        string state = context.Request.Query["state"].ToString();
        if (string.IsNullOrEmpty(state) || state != Auth.GetState())
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await WriteJsonResponse(
                context.Response,
                new { status = "error", message = "Invalid state parameter" }
            );
            return;
        }

        _state.TransitionTo(SetupPhase.Authenticating);

        string redirectUri = BuildRedirectUri(context.Request);
        string codeVerifier = _codeVerifier;

        // Exchange the authorization code for tokens synchronously so they are
        // persisted before the response is sent.  This prevents token loss if
        // the process is killed (e.g. port-conflict auto-kill) before the
        // background task finishes.
        string responseTitle;
        string responseMessage;
        bool responseIsError;

        try
        {
            await Auth.TokenByAuthorizationCode(code, codeVerifier, redirectUri);

            if (string.IsNullOrEmpty(Globals.Globals.AccessToken))
                throw new("Token exchange succeeded but access token was not stored");

            if (!File.Exists(AppFiles.TokenFile))
                throw new("Token exchange succeeded but token file was not written");

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
            _state.SetError("Sign in failed. Please try again.");
            _state.TransitionTo(SetupPhase.Unauthenticated);

            responseTitle = "Authentication Failed";
            responseMessage = "Sign in failed. Please try again.";
            responseIsError = true;
        }
        finally
        {
            _codeVerifier = Auth.GenerateCodeVerifier();
            _codeChallenge = Auth.GenerateCodeChallenge(_codeVerifier);
            
            string stateParam = Auth.GenerateCodeVerifier();
            Auth.SetState(stateParam);
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(
            BuildCallbackHtml(responseTitle, responseMessage, responseIsError)
        );
        await context.Response.CompleteAsync();

        // Run post-auth registration in the background (networking, cert, etc.)
        // The response is already sent and tokens are persisted, so this is safe.
        if (!responseIsError)
        {
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
                + $"<h2>{WebUtility.HtmlEncode(title)}</h2>"
                + $"<p>{WebUtility.HtmlEncode(message)}</p>"
                + "<p style=\"margin-top:16px;color:#666;\">Redirecting to setup...</p>"
                + "</div>"
                + "<script>setTimeout(function(){window.location.href='/setup';}, 1500);</script>"
                + "</body></html>";
        }

        // Success case: try to close the tab (works when opened as popup),
        // otherwise show a static message since the server will restart for HTTPS
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
            + $"<h2>{WebUtility.HtmlEncode(title)}</h2>"
            + $"<p>{WebUtility.HtmlEncode(message)}</p>"
            + "<p id=\"status\" style=\"margin-top:16px;color:#666;\">You can close this tab.</p>"
            + "</div>"
            + "<script>"
            + "try{window.close();}catch(e){}"
            + "document.getElementById('status').textContent="
            + "'Server is restarting with HTTPS. You can close this tab.';"
            + "</script>"
            + "</body></html>";
    }

    internal static string BuildRedirectUri(HttpRequest request)
    {
        int port = request.Host.Port ?? Config.InternalServerPort;
        return $"http://localhost:{port}/sso-callback";
    }

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

    internal async Task HandleQrCode(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Get)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        string data = context.Request.Query["data"].ToString();

        if (string.IsNullOrEmpty(data))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        byte[] pngBytes = GenerateQrCodePng(data);

        context.Response.ContentType = "image/png";
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentLength = pngBytes.Length;
        await context.Response.Body.WriteAsync(pngBytes);
    }

    internal async Task HandleDeviceCode(HttpContext context)
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
            List<KeyValuePair<string, string>> deviceCodeBody = Auth.BuildDeviceCodeRequestBody(
                Config.TokenClientId
            );

            GenericHttpClient authClient = new(Config.AuthBaseUrl);
            authClient.SetDefaultHeaders(Config.UserAgent);
            string deviceCodeResponse = await authClient.SendAndReadAsync(
                HttpMethod.Post,
                "protocol/openid-connect/auth/device",
                new FormUrlEncodedContent(deviceCodeBody)
            );

            Dto.DeviceAuthResponse deviceData =
                deviceCodeResponse.FromJson<Dto.DeviceAuthResponse>()
                ?? throw new("Failed to get device code");

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

            // Show the terminal UI for console/binary mode users who also have the
            // browser open. Both paths run simultaneously — whichever completes first wins.
            if (SetupTerminalUi.IsInteractiveTerminal)
            {
                _terminalUi ??= new SetupTerminalUi();
                string setupPageUrl = $"http://localhost:{_port}/setup";
                _terminalUi.Show(
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

    private async Task HandleRetry(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Post)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        context.Response.ContentType = "application/json";

        SetupPhase phase = _state.CurrentPhase;

        // Re-authentication is not required if we already have tokens.
        // Reset any error state so the UI can show progress again.
        if (phase == SetupPhase.Authenticated || phase == SetupPhase.Registering)
        {
            _state.ClearError();

            context.Response.StatusCode = StatusCodes.Status200OK;
            await WriteJsonResponse(context.Response, new { status = "retrying" });

            // Kick off registration in the background — response is already sent.
            _ = Task.Run(async () =>
            {
                try
                {
                    // Reset to Authenticated so RunPostAuthRegistration will proceed.
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

        // Any other phase: tell the client to go back to login.
        context.Response.StatusCode = StatusCodes.Status200OK;
        await WriteJsonResponse(context.Response, new { status = "unauthenticated" });
    }

    private async Task PollDeviceGrant(Dto.DeviceAuthResponse deviceData)
    {
        if (string.IsNullOrEmpty(Config.TokenClientId))
            return;

        _state.TransitionTo(SetupPhase.Authenticating);

        List<KeyValuePair<string, string>> tokenBody = Auth.BuildDeviceTokenBody(
            Config.TokenClientId,
            deviceData.DeviceCode
        );

        DateTime expiresAt = DateTime.Now.AddSeconds(deviceData.ExpiresIn);
        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);

        while (DateTime.Now < expiresAt)
        {
            await Task.Delay(deviceData.Interval * 1000);

            try
            {
                using HttpResponseMessage response = await authClient.SendAsync(
                    HttpMethod.Post,
                    "protocol/openid-connect/token",
                    new FormUrlEncodedContent(tokenBody)
                );

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    Dto.AuthResponse data =
                        content.FromJson<Dto.AuthResponse>()
                        ?? throw new("Failed to deserialize token response");
                    Auth.SetTokensFromSetup(data);
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

    internal async Task RunPostAuthRegistration()
    {
        if (_state.CurrentPhase != SetupPhase.Authenticated)
            return;

        try
        {
            // Wait for database schema to be ready before registration queries it
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

    private static async Task HandleServiceUnavailable(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        object response = new
        {
            status = "setup_required",
            message = "Server is in setup mode",
            setup_url = "/setup",
        };

        await WriteJsonResponse(context.Response, response);
    }

    private static async Task WriteJsonResponse(HttpResponse response, object data)
    {
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        await response.WriteAsync(json, Encoding.UTF8);
    }

    internal static string LoadSetupHtml()
    {
        if (_cachedSetupHtml != null)
            return _cachedSetupHtml;

        Assembly assembly = typeof(SetupServer).Assembly;
        string resourceName =
            assembly
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("setup.html", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Embedded setup.html resource not found");

        using Stream stream =
            assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Failed to load embedded resource: {resourceName}"
            );
        using StreamReader reader = new(stream, Encoding.UTF8);
        _cachedSetupHtml = reader.ReadToEnd();

        return _cachedSetupHtml;
    }

    internal static byte[] GenerateQrCodePng(string data)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData qrData = generator.CreateQrCode(data, QRCodeGenerator.ECCLevel.L);
        using PngByteQRCode qrCode = new(qrData);
        return qrCode.GetGraphic(8);
    }

    internal static void ClearHtmlCache()
    {
        _cachedSetupHtml = null;
    }
}
