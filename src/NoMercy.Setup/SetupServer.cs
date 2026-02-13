using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Newtonsoft.Json;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using QRCoder;
using Serilog.Events;

namespace NoMercy.Setup;

public class SetupServer
{
    private readonly SetupState _state;
    private IWebHost? _host;
    private readonly int _port;

    private static string? _cachedSetupHtml;

    private string _codeVerifier;
    private string _codeChallenge;

    public bool IsRunning { get; private set; }

    public SetupServer(SetupState state, int? port = null)
    {
        _state = state;
        _port = port ?? Config.InternalServerPort;
        _codeVerifier = Auth.GenerateCodeVerifier();
        _codeChallenge = Auth.GenerateCodeChallenge(_codeVerifier);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        _host = WebHost.CreateDefaultBuilder()
            .ConfigureKestrel(kestrelOptions =>
            {
                kestrelOptions.Listen(IPAddress.Any, _port, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1;
                });
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.Run(HandleRequest);
            })
            .Build();

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
            _host.Dispose();
            _host = null;
            IsRunning = false;

            Logger.Setup("Setup server stopped");
        }
    }

    internal async Task HandleRequest(HttpContext context)
    {
        string path = context.Request.Path.Value?.TrimEnd('/') ?? "";

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
            auth_base_url = Config.AuthBaseUrl ?? "",
            client_id = Config.TokenClientId ?? "",
            code_challenge = _codeChallenge
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
            string message = string.IsNullOrEmpty(errorDescription)
                ? $"Authorization failed: {error}"
                : $"Authorization failed: {errorDescription}";

            Logger.Setup($"OAuth callback error: {error} — {errorDescription}",
                LogEventLevel.Warning);

            _state.SetError(message);

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(BuildCallbackHtml(
                "Authorization Failed",
                message,
                isError: true));
            return;
        }

        string code = context.Request.Query["code"].ToString();

        if (string.IsNullOrEmpty(code))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await WriteJsonResponse(context.Response, new
            {
                status = "error",
                message = "Missing authorization code"
            });
            return;
        }

        _state.TransitionTo(SetupPhase.Authenticating);

        string redirectUri = BuildRedirectUri(context.Request);

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(BuildCallbackHtml(
            "Authentication Received",
            "Exchanging authorization code for tokens..."));
        await context.Response.CompleteAsync();

        string codeVerifier = _codeVerifier;

        _ = Task.Run(async () =>
        {
            try
            {
                await Auth.TokenByAuthorizationCode(code, codeVerifier, redirectUri);

                if (string.IsNullOrEmpty(Globals.Globals.AccessToken))
                    throw new("Token exchange succeeded but access token was not stored");

                if (!File.Exists(AppFiles.TokenFile))
                    throw new("Token exchange succeeded but token file was not written");

                _state.TransitionTo(SetupPhase.Authenticated);
                Logger.Setup("OAuth token exchange completed successfully");

                await RunPostAuthRegistration();
            }
            catch (Exception ex)
            {
                _state.SetError($"Authentication failed: {ex.Message}");
                _state.TransitionTo(SetupPhase.Unauthenticated);
                Logger.Setup($"OAuth token exchange failed: {ex.Message}",
                    LogEventLevel.Error);
            }
            finally
            {
                _codeVerifier = Auth.GenerateCodeVerifier();
                _codeChallenge = Auth.GenerateCodeChallenge(_codeVerifier);
            }
        });
    }

    internal static string BuildCallbackHtml(string title, string message,
        bool isError = false)
    {
        string color = isError ? "#f08080" : "#CBAFFF";
        string redirect = isError
            ? "window.location.href='/setup';"
            : "window.location.href='/setup';";
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
               + $"<script>setTimeout(function(){{{redirect}}}, 1500);</script>"
               + "</body></html>";
    }

    internal static string BuildRedirectUri(HttpRequest request)
    {
        string scheme = request.Scheme;
        string host = request.Host.ToString();
        return $"{scheme}://{host}/sso-callback";
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
            error = _state.ErrorMessage
        };
    }

    private static async Task WriteSseEvent(
        HttpResponse response, string data, CancellationToken cancellationToken)
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
            await WriteJsonResponse(context.Response, new
            {
                error = true,
                message = "Auth configuration not available"
            });
            return;
        }

        try
        {
            List<KeyValuePair<string, string>> deviceCodeBody =
                Auth.BuildDeviceCodeRequestBody(Config.TokenClientId);

            GenericHttpClient authClient = new(Config.AuthBaseUrl);
            authClient.SetDefaultHeaders(Config.UserAgent);
            string deviceCodeResponse = await authClient.SendAndReadAsync(
                HttpMethod.Post,
                "protocol/openid-connect/auth/device",
                new FormUrlEncodedContent(deviceCodeBody));

            Dto.DeviceAuthResponse deviceData =
                deviceCodeResponse.FromJson<Dto.DeviceAuthResponse>()
                ?? throw new("Failed to get device code");

            context.Response.StatusCode = StatusCodes.Status200OK;
            await WriteJsonResponse(context.Response, new
            {
                user_code = deviceData.UserCode,
                verification_uri = deviceData.VerificationUri,
                verification_uri_complete = deviceData.VerificationUriComplete,
                expires_in = deviceData.ExpiresIn,
                interval = deviceData.Interval
            });

            _ = Task.Run(async () =>
            {
                await PollDeviceGrant(deviceData);
            });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await WriteJsonResponse(context.Response, new
            {
                error = true,
                message = $"Failed to initiate device login: {ex.Message}"
            });
        }
    }

    private async Task PollDeviceGrant(Dto.DeviceAuthResponse deviceData)
    {
        if (string.IsNullOrEmpty(Config.TokenClientId))
            return;

        _state.TransitionTo(SetupPhase.Authenticating);

        List<KeyValuePair<string, string>> tokenBody =
            Auth.BuildDeviceTokenBody(Config.TokenClientId, deviceData.DeviceCode);

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
                    new FormUrlEncodedContent(tokenBody));

                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    Dto.AuthResponse data = content.FromJson<Dto.AuthResponse>()
                                            ?? throw new("Failed to deserialize token response");
                    Auth.SetTokensFromSetup(data);
                    _state.TransitionTo(SetupPhase.Authenticated);

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
            _state.TransitionTo(SetupPhase.Registering);

            await Networking.Networking.Discover();
            await Register.Init();

            _state.TransitionTo(SetupPhase.Registered);

            if (Certificate.HasValidCertificate())
            {
                _state.TransitionTo(SetupPhase.CertificateAcquired);
                _state.TransitionTo(SetupPhase.Complete);
                Logger.Setup("Setup complete — server will restart with HTTPS");
            }
            else
            {
                _state.SetError("Registration completed but certificate was not acquired");
            }
        }
        catch (Exception ex)
        {
            _state.SetError($"Registration failed: {ex.Message}");
            _state.TransitionTo(SetupPhase.Authenticated);
            Logger.Setup($"Post-auth registration failed: {ex.Message}",
                LogEventLevel.Error);
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
            setup_url = "/setup"
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
        string resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("setup.html", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Embedded setup.html resource not found");

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
                              ?? throw new InvalidOperationException(
                                  $"Failed to load embedded resource: {resourceName}");
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
