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
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NoMercy.Api.Middleware;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;
using Xunit;

namespace NoMercy.Tests.Api.Middleware;

[Trait(name: "Category", value: "Unit")]
public class HmacValidationMiddlewareTests
{
    private const string Secret = "test-hmac-secret-middleware";

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal test pipeline:
    ///   HmacValidationMiddleware → terminal that returns 200 OK.
    /// </summary>
    private static HttpClient BuildClient(string? configuredSecret)
    {
        IHost host = new HostBuilder()
            .ConfigureWebHost(configure: web =>
            {
                web.UseTestServer();
                web.ConfigureServices(configureServices: services =>
                {
                    EncoderOptions opts = new()
                    {
                        DistributedEncodingSigningKey = configuredSecret,
                    };
                    services.AddSingleton<IOptions<EncoderOptions>>(
                        implementationInstance: new OptionsWrapper<EncoderOptions>(options: opts)
                    );
                });
                web.Configure(configureApp: app =>
                {
                    app.UseMiddleware<HmacValidationMiddleware>();
                    app.Run(handler: ctx =>
                    {
                        ctx.Response.StatusCode = 200;
                        return ctx.Response.WriteAsync(text: "ok");
                    });
                });
            })
            .Build();

        host.Start();
        return host.GetTestClient();
    }

    /// <summary>
    /// Same pipeline as <see cref="BuildClient"/>, plus a registered
    /// <see cref="ILicenseTokenClient"/> — exercises the remote-worker token
    /// introspection branch (Phase 4.9): when a request carries
    /// X-NoMercy-WorkerToken, the middleware checks IntrospectAsync before
    /// falling back to the configured signing key, and the token itself
    /// becomes the HMAC secret when active.
    /// </summary>
    private static HttpClient BuildClientWithWorkerToken(
        string? configuredSecret,
        Mock<ILicenseTokenClient> licenseTokenClient
    )
    {
        IHost host = new HostBuilder()
            .ConfigureWebHost(configure: web =>
            {
                web.UseTestServer();
                web.ConfigureServices(configureServices: services =>
                {
                    EncoderOptions opts = new()
                    {
                        DistributedEncodingSigningKey = configuredSecret,
                    };
                    services.AddSingleton<IOptions<EncoderOptions>>(
                        implementationInstance: new OptionsWrapper<EncoderOptions>(options: opts)
                    );
                    services.AddSingleton(implementationInstance: licenseTokenClient.Object);
                });
                web.Configure(configureApp: app =>
                {
                    app.UseMiddleware<HmacValidationMiddleware>();
                    app.Run(handler: ctx =>
                    {
                        ctx.Response.StatusCode = 200;
                        return ctx.Response.WriteAsync(text: "ok");
                    });
                });
            })
            .Build();

        host.Start();
        return host.GetTestClient();
    }

    private static (long timestamp, string signature) MakeSignature(
        string method,
        string path,
        byte[] body,
        string secret
    )
    {
        HmacSigner signer = new(secret: secret);
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string sig = signer.Sign(method: method, path: path, timestamp: ts, body: body);
        return (ts, sig);
    }

    // -------------------------------------------------------------------------
    // Tests: non-protected routes pass through
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(data: "/api/v1/media/movies")]
    [InlineData(data: "/api/v1/auth/login")]
    [InlineData(data: "/health")]
    public async Task NonProtectedRoute_NoHeaders_Returns200(string route)
    {
        HttpClient client = BuildClient(configuredSecret: Secret);
        HttpResponseMessage response = await client.GetAsync(requestUri: route);
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Tests: protected routes without headers → 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DistributionRoute_NoTimestamp_Returns401()
    {
        HttpClient client = BuildClient(configuredSecret: Secret);
        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/distribution/workers");

        Assert.Equal(expected: HttpStatusCode.Unauthorized, actual: response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.Equal(expected: "hmac_invalid", actual: json.RootElement.GetProperty(propertyName: "error").GetString());
        Assert.Equal(expected: "missing_timestamp", actual: json.RootElement.GetProperty(propertyName: "reason").GetString());
    }

    [Fact]
    public async Task WorkerRoute_NoSignature_Returns401()
    {
        HttpClient client = BuildClient(configuredSecret: Secret);
        HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: "/api/v1/worker/execute-task");
        request.Headers.Add(
            name: "X-NoMercy-Timestamp",
            value: DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        );

        HttpResponseMessage response = await client.SendAsync(request: request);

        Assert.Equal(expected: HttpStatusCode.Unauthorized, actual: response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.Equal(expected: "missing_signature", actual: json.RootElement.GetProperty(propertyName: "reason").GetString());
    }

    [Fact]
    public async Task DistributionRoute_WrongSecret_Returns401()
    {
        HttpClient client = BuildClient(configuredSecret: Secret);

        byte[] bodyBytes = Encoding.UTF8.GetBytes(s: "{\"test\":true}");
        (long ts, string sig) = MakeSignature(
            method: "POST",
            path: "/api/v1/distribution/tasks",
            body: bodyBytes,
            secret: "wrong-secret"
        );

        HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: "/api/v1/distribution/tasks")
        {
            Content = new ByteArrayContent(content: bodyBytes),
        };
        request.Headers.Add(name: "X-NoMercy-Timestamp", value: ts.ToString());
        request.Headers.Add(name: "X-NoMercy-Signature", value: sig);

        HttpResponseMessage response = await client.SendAsync(request: request);

        Assert.Equal(expected: HttpStatusCode.Unauthorized, actual: response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.Equal(expected: "signature_invalid", actual: json.RootElement.GetProperty(propertyName: "reason").GetString());
    }

    [Fact]
    public async Task DistributionRoute_StaleTimestamp_Returns401()
    {
        HttpClient client = BuildClient(configuredSecret: Secret);

        byte[] bodyBytes = [];
        long staleTs = DateTimeOffset.UtcNow.AddMinutes(minutes: -6).ToUnixTimeSeconds();
        HmacSigner signer = new(secret: Secret);
        string sig = signer.Sign(method: "GET", path: "/api/v1/distribution/workers", timestamp: staleTs, body: bodyBytes);

        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: "/api/v1/distribution/workers");
        request.Headers.Add(name: "X-NoMercy-Timestamp", value: staleTs.ToString());
        request.Headers.Add(name: "X-NoMercy-Signature", value: sig);

        HttpResponseMessage response = await client.SendAsync(request: request);

        Assert.Equal(expected: HttpStatusCode.Unauthorized, actual: response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Tests: happy path → 200
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DistributionRoute_ValidSignature_Returns200()
    {
        HttpClient client = BuildClient(configuredSecret: Secret);

        byte[] bodyBytes = Encoding.UTF8.GetBytes(s: "{\"test\":true}");
        (long ts, string sig) = MakeSignature(
            method: "POST",
            path: "/api/v1/distribution/tasks",
            body: bodyBytes,
            secret: Secret
        );

        HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: "/api/v1/distribution/tasks")
        {
            Content = new ByteArrayContent(content: bodyBytes),
        };
        request.Headers.Add(name: "X-NoMercy-Timestamp", value: ts.ToString());
        request.Headers.Add(name: "X-NoMercy-Signature", value: sig);

        HttpResponseMessage response = await client.SendAsync(request: request);

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    [Fact]
    public async Task WorkerRoute_ValidSignature_EmptyBody_Returns200()
    {
        HttpClient client = BuildClient(configuredSecret: Secret);

        byte[] bodyBytes = [];
        (long ts, string sig) = MakeSignature(
            method: "POST",
            path: "/api/v1/worker/execute-task",
            body: bodyBytes,
            secret: Secret
        );

        HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: "/api/v1/worker/execute-task");
        request.Headers.Add(name: "X-NoMercy-Timestamp", value: ts.ToString());
        request.Headers.Add(name: "X-NoMercy-Signature", value: sig);

        HttpResponseMessage response = await client.SendAsync(request: request);

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Tests: progress-push exemption
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(data: "/api/v1/distribution/workers/worker-1/tasks/task-abc/progress")]
    [InlineData(data: "/api/v1/distribution/workers/xyz/tasks/123/progress")]
    public async Task ProgressPushPath_NoHeaders_IsExempt_Returns200(string path)
    {
        HttpClient client = BuildClient(configuredSecret: Secret);
        HttpResponseMessage response = await client.PostAsync(requestUri: path, content: new StringContent(content: "{}"));
        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Tests: no signing key configured → 503 (misconfigured, not silent pass-through)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NoSigningKeyConfigured_ProtectedRoute_Returns503()
    {
        // When no signing key is configured, the middleware rejects protected
        // routes with 503 rather than passing through silently — a missing key
        // means distributed encoding is misconfigured, and passing through would
        // bypass HMAC authentication entirely.
        HttpClient client = BuildClient(configuredSecret: null);
        HttpResponseMessage response = await client.GetAsync(requestUri: "/api/v1/distribution/workers");
        Assert.Equal(expected: HttpStatusCode.ServiceUnavailable, actual: response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Tests: remote-worker token introspection (ILicenseTokenClient)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WorkerToken_ActiveToken_UsesTokenItselfAsHmacSecret()
    {
        const string workerToken = "worker-token-abc123";
        Mock<ILicenseTokenClient> licenseTokenClient = new();
        licenseTokenClient
            .Setup(expression: c => c.IntrospectAsync(workerToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new IntrospectResult(Active: true, Scopes: [], Message: null));

        // Configured signing key is deliberately different from the worker
        // token — proves the signature must be verified against the WORKER
        // TOKEN, not the static DistributedEncodingSigningKey.
        HttpClient client = BuildClientWithWorkerToken(
            configuredSecret: "static-secret-never-used-here",
            licenseTokenClient: licenseTokenClient
        );

        byte[] bodyBytes = Encoding.UTF8.GetBytes(s: "{\"task\":true}");
        HmacSigner signer = new(secret: workerToken);
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string sig = signer.Sign(method: "POST", path: "/api/v1/worker/execute-task", timestamp: ts, body: bodyBytes);

        HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: "/api/v1/worker/execute-task")
        {
            Content = new ByteArrayContent(content: bodyBytes),
        };
        request.Headers.Add(name: "X-NoMercy-WorkerToken", value: workerToken);
        request.Headers.Add(name: "X-NoMercy-Timestamp", value: ts.ToString());
        request.Headers.Add(name: "X-NoMercy-Signature", value: sig);

        HttpResponseMessage response = await client.SendAsync(request: request);

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        licenseTokenClient.Verify(
            expression: c => c.IntrospectAsync(workerToken, It.IsAny<CancellationToken>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task WorkerToken_ActiveToken_ButSignedWithWrongSecret_Returns401()
    {
        const string workerToken = "worker-token-abc123";
        Mock<ILicenseTokenClient> licenseTokenClient = new();
        licenseTokenClient
            .Setup(expression: c => c.IntrospectAsync(workerToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new IntrospectResult(Active: true, Scopes: [], Message: null));

        HttpClient client = BuildClientWithWorkerToken(configuredSecret: Secret, licenseTokenClient: licenseTokenClient);

        byte[] bodyBytes = [];
        HmacSigner wrongSigner = new(secret: "not-the-worker-token");
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string sig = wrongSigner.Sign(method: "GET", path: "/api/v1/worker/status", timestamp: ts, body: bodyBytes);

        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: "/api/v1/worker/status");
        request.Headers.Add(name: "X-NoMercy-WorkerToken", value: workerToken);
        request.Headers.Add(name: "X-NoMercy-Timestamp", value: ts.ToString());
        request.Headers.Add(name: "X-NoMercy-Signature", value: sig);

        HttpResponseMessage response = await client.SendAsync(request: request);

        Assert.Equal(expected: HttpStatusCode.Unauthorized, actual: response.StatusCode);
    }

    [Fact]
    public async Task WorkerToken_InactiveToken_Returns401WorkerTokenInvalid()
    {
        const string workerToken = "revoked-worker-token";
        Mock<ILicenseTokenClient> licenseTokenClient = new();
        licenseTokenClient
            .Setup(expression: c => c.IntrospectAsync(workerToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new IntrospectResult(Active: false, Scopes: [], Message: "Token has been revoked"));

        HttpClient client = BuildClientWithWorkerToken(configuredSecret: Secret, licenseTokenClient: licenseTokenClient);

        HttpRequestMessage request = new(method: HttpMethod.Get, requestUri: "/api/v1/worker/status");
        request.Headers.Add(name: "X-NoMercy-WorkerToken", value: workerToken);
        request.Headers.Add(
            name: "X-NoMercy-Timestamp",
            value: DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        );
        request.Headers.Add(name: "X-NoMercy-Signature", value: "irrelevant-since-token-is-inactive");

        HttpResponseMessage response = await client.SendAsync(request: request);

        Assert.Equal(expected: HttpStatusCode.Unauthorized, actual: response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(json: body);
        Assert.Equal(expected: "worker_token_invalid", actual: json.RootElement.GetProperty(propertyName: "reason").GetString());
    }

    [Fact]
    public async Task NoWorkerTokenHeader_FallsBackToConfiguredSigningKey_NeverConsultsLicenseClient()
    {
        Mock<ILicenseTokenClient> licenseTokenClient = new();
        HttpClient client = BuildClientWithWorkerToken(configuredSecret: Secret, licenseTokenClient: licenseTokenClient);

        byte[] bodyBytes = Encoding.UTF8.GetBytes(s: "{\"test\":true}");
        (long ts, string sig) = MakeSignature(
            method: "POST",
            path: "/api/v1/distribution/tasks",
            body: bodyBytes,
            secret: Secret
        );

        HttpRequestMessage request = new(method: HttpMethod.Post, requestUri: "/api/v1/distribution/tasks")
        {
            Content = new ByteArrayContent(content: bodyBytes),
        };
        request.Headers.Add(name: "X-NoMercy-Timestamp", value: ts.ToString());
        request.Headers.Add(name: "X-NoMercy-Signature", value: sig);

        HttpResponseMessage response = await client.SendAsync(request: request);

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        licenseTokenClient.Verify(
            expression: c => c.IntrospectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times: Times.Never
        );
    }
}
