using System.Net;
using System.Text;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Setup;

public class SetupServer
{
    private readonly SetupState _state;
    private IWebHost? _host;
    private readonly int _port;

    public bool IsRunning { get; private set; }

    public SetupServer(SetupState state, int? port = null)
    {
        _state = state;
        _port = port ?? Config.InternalServerPort;
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

            case "/sso-callback":
                await HandleSsoCallback(context);
                break;

            case "/setup/status":
                await HandleSetupStatus(context);
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

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status200OK;

        object response = new
        {
            status = "setup_required",
            phase = _state.CurrentPhase.ToString(),
            error = _state.ErrorMessage,
            server_port = _port
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

        context.Response.ContentType = "text/html";
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync(
            "<html><body><p>Authentication received. You can close this window.</p>" +
            "<script>window.close();</script></body></html>");
        await context.Response.CompleteAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await Auth.TokenByAuthorizationCode(code);
                _state.TransitionTo(SetupPhase.Authenticated);
            }
            catch (Exception ex)
            {
                _state.SetError($"Authentication failed: {ex.Message}");
                _state.TransitionTo(SetupPhase.Unauthenticated);
            }
        });
    }

    private async Task HandleSetupStatus(HttpContext context)
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
            phase = _state.CurrentPhase.ToString(),
            is_setup_required = _state.IsSetupRequired,
            is_authenticated = _state.IsAuthenticated,
            error = _state.ErrorMessage
        };

        await WriteJsonResponse(context.Response, response);
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
}
