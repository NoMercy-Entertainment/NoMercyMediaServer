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
using System.Net.Sockets;
using System.Text;

namespace NoMercy.Tests.Setup.Infrastructure;

/// <summary>
/// A real local HTTP server bound to 127.0.0.1 on an ephemeral port. Several
/// NoMercy.Setup classes (AuthManager, ServerRegistrationService, BootOrchestrator,
/// CastSessionTokenService, ApiKeyLoader) construct their own <c>new HttpClient()</c>
/// inline rather than accepting an injectable one, so a fake <see cref="HttpMessageHandler"/>
/// cannot reach them. Pointing <c>ExternalServicesConfig.Current.*BaseUrl</c> at this
/// server exercises the real HTTP call over loopback — a real input surface, not a mock
/// of the class under test — without ever touching the live internet or a real Keycloak.
/// </summary>
public sealed class LoopbackHttpServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private int _requestCount;

    /// <summary>
    /// Called once per request on a background thread. Return the response to send.
    /// Defaults to a 404 so an un-configured server fails loudly instead of hanging.
    /// </summary>
    public Func<LoopbackRequest, LoopbackResponse> Handler { get; set; } =
        _ => new(StatusCode: 404, Body: "not found");

    public int RequestCount => Volatile.Read(location: ref _requestCount);

    /// <summary>Base URL including trailing slash, e.g. "http://127.0.0.1:54321/".</summary>
    public string BaseUrl { get; }

    public LoopbackHttpServer()
    {
        int port = GetFreeLoopbackPort();
        BaseUrl = $"http://127.0.0.1:{port}/";

        _listener = new();
        _listener.Prefixes.Add(uriPrefix: BaseUrl);
        _listener.Start();

        _acceptLoop = Task.Run(function: AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken: _cts.Token);
            }
            catch (Exception)
            {
                // Listener stopped/disposed or the wait was cancelled — exit quietly.
                return;
            }

            Interlocked.Increment(location: ref _requestCount);

            try
            {
                string body;
                using (StreamReader reader = new(stream: context.Request.InputStream, encoding: Encoding.UTF8))
                    body = await reader.ReadToEndAsync();

                LoopbackRequest request = new(
                    Method: context.Request.HttpMethod,
                    Path: context.Request.Url?.AbsolutePath ?? "/",
                    Query: context.Request.QueryString,
                    Body: body
                );

                LoopbackResponse response = Handler(arg: request);

                if (response.Abort)
                {
                    // Simulates a real network-level failure (connection reset) rather
                    // than a well-formed HTTP error status — the client sees an
                    // IOException/HttpRequestException, not a parseable response.
                    context.Response.Abort();
                    continue;
                }

                context.Response.StatusCode = response.StatusCode;
                context.Response.ContentType = response.ContentType;
                byte[] bytes = Encoding.UTF8.GetBytes(s: response.Body);
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(buffer: bytes);
                context.Response.OutputStream.Close();
            }
            catch (Exception)
            {
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.OutputStream.Close();
                }
                catch (Exception)
                {
                    // Best-effort — the client may have already disconnected.
                }
            }
        }
    }

    private static int GetFreeLoopbackPort()
    {
        TcpListener probe = new(localaddr: IPAddress.Loopback, port: 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _listener.Stop();
        }
        catch (Exception)
        {
            // Already stopped.
        }
        _listener.Close();
        try
        {
            _acceptLoop.Wait(timeout: TimeSpan.FromSeconds(seconds: 2));
        }
        catch (Exception)
        {
            // Best-effort shutdown — the loop's own catch returns as soon as the
            // listener is closed.
        }
        _cts.Dispose();
    }
}

/// <summary>A single request observed by <see cref="LoopbackHttpServer"/>.</summary>
public sealed record LoopbackRequest(
    string Method,
    string Path,
    System.Collections.Specialized.NameValueCollection Query,
    string Body
);

/// <summary>The response <see cref="LoopbackHttpServer.Handler"/> should send back.</summary>
public sealed record LoopbackResponse(
    int StatusCode,
    string Body,
    string ContentType = "application/json"
)
{
    /// <summary>
    /// When true, the connection is aborted instead of a response being written —
    /// the client observes a real network-level exception (not a parseable HTTP
    /// status), exactly like a dropped connection or reset mid-request.
    /// </summary>
    public bool Abort { get; init; }

    public static LoopbackResponse Aborted() => new(StatusCode: 0, Body: string.Empty) { Abort = true };
}
