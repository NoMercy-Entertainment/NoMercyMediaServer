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
using NoMercy.Providers.Helpers;
using NoMercy.Providers.TVDB.Client;
using NoMercy.Providers.TVDB.Models.Auth;
using NoMercy.Tests.Common.Providers;

namespace NoMercy.Tests.Providers.TVDB.Client;

/// <summary>
/// Requirement-driven coverage for <see cref="TvdbBaseClient"/>'s login,
/// request-building and error/retry contract, exercised through the mock HTTP
/// harness. Prior TVDB coverage (<see cref="TvdbBaseClientTests"/>) only
/// inspected the compiled IL of <c>LoginAsync</c> for a stray <c>.Result</c>
/// call — it never scripted a request or asserted on a response, so it guards
/// a single historical regression without exercising the actual contract.
///
/// <see cref="TvdbBaseClient"/> caches its login token in a <c>private static</c>
/// field shared by every instance for the lifetime of the process (see
/// <see cref="TvdbTokenAccess"/>), so every test here resets it first —
/// otherwise a previous test's cached token would silently skip the login
/// flow this class is meant to verify.
/// </summary>
[Collection("HttpClientProvider")]
public sealed class TvdbBaseClientHarnessTests : ProviderHttpHarness
{
    public TvdbBaseClientHarnessTests()
        : base([HttpClientNames.Tvdb, HttpClientNames.TvdbLogin])
    {
        TvdbTokenAccess.Reset();
    }

    public override void Dispose()
    {
        TvdbTokenAccess.Reset();
        base.Dispose();
    }

    private sealed class TestableClient() : TvdbBaseClient(0, "nld")
    {
        public new Task<T?> Get<T>(
            string url,
            Dictionary<string, string?>? query = null,
            bool? priority = false,
            bool skipCache = false,
            TimeSpan? maxCacheAge = null
        )
            where T : class => base.Get<T>(url, query, priority, skipCache, maxCacheAge);
    }

    private static TvdbLoginResponse LoginBody(string token) =>
        new()
        {
            Status = "success",
            Data = new() { Token = token, ExpiresAt = DateTime.UtcNow.AddMonths(1) },
        };

    [Fact]
    public async Task Get_NoApiKeyConfigured_ReturnsNullWithoutAnyHttpCall()
    {
        // Requirement: a missing TVDB API key must fail closed (null) before
        // any request is attempted, not surface as a network/auth error.
        TestApiKeyStore.Instance.TvdbKey = "";

        using TestableClient client = new();
        string? result = await client.Get<string>(Unique("series"));

        result.Should().BeNull();
        Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_ValidApiKey_LogsInThenSendsBearerAndAcceptLanguageHeaders()
    {
        // Requirement: the FIRST call with no cached token logs in via the
        // dedicated TvdbLogin client, then the actual GET must carry the
        // resulting bearer token plus the client's configured Accept-Language.
        TestApiKeyStore.Instance.TvdbKey = "test-tvdb-key";
        Handler.WhenPost(
            "login",
            MockResponse.Json(HttpStatusCode.OK, LoginBody("session-token-abc"))
        );

        string path = Unique("series");
        Handler.WhenGet(path, MockResponse.Json(HttpStatusCode.OK, "\"ok\""));

        using TestableClient client = new();
        string? result = await client.Get<string>(path);

        result.Should().Be("ok");

        Handler.Requests.Should().ContainSingle(r => r.Path.Contains("login"));
        CapturedRequest getRequest = Handler
            .Requests.Should()
            .ContainSingle(r => r.Path.Contains(path))
            .Which;
        getRequest.HeaderValue("Authorization").Should().Be("Bearer session-token-abc");
        getRequest.HeaderValue("Accept-Language").Should().Be("nld");
    }

    [Fact]
    public async Task Get_TokenAlreadyCached_SkipsLoginOnSubsequentCall()
    {
        // Requirement: EnsureAuthenticatedAsync must not re-login while the
        // cached token is still valid (TVDB tokens last ~1 month).
        TvdbTokenAccess.Set(LoginBody("cached-token"));

        string path = Unique("series");
        Handler.WhenGet(path, MockResponse.Json(HttpStatusCode.OK, "\"ok\""));

        using TestableClient client = new();
        string? result = await client.Get<string>(path);

        result.Should().Be("ok");
        Handler.Requests.Should().NotContain(r => r.Path.Contains("login"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task Get_ClientErrorStatuses_SoftFailToNull(HttpStatusCode status)
    {
        TvdbTokenAccess.Set(LoginBody("cached-token"));
        string path = Unique("series");
        Handler.WhenGet(path, MockResponse.Status(status));

        using TestableClient client = new();
        string? result = await client.Get<string>(path);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Get_MalformedJsonBody_ReturnsNullInsteadOfThrowing()
    {
        TvdbTokenAccess.Set(LoginBody("cached-token"));
        string path = Unique("series");
        Handler.WhenGet(path, MockResponse.Malformed());

        using TestableClient client = new();
        string? result = await client.Get<string>(path);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Get_TransientTooManyRequests_RetriesOnceThenReturnsData()
    {
        // Requirement: same shared Queue retry contract as every other
        // provider — a transient 429 is retried before the caller sees it.
        TvdbTokenAccess.Set(LoginBody("cached-token"));
        string path = Unique("series");
        Handler.WhenGet(
            path,
            [
                MockResponse.Status(HttpStatusCode.TooManyRequests),
                MockResponse.Json(HttpStatusCode.OK, "\"recovered\""),
            ]
        );

        using TestableClient client = new();
        string? result = await client.Get<string>(path);

        result.Should().Be("recovered");
        Handler.RequestCountFor(path).Should().Be(2);
    }

    [Fact]
    public async Task Get_UnauthorizedMidFlight_ClearsCachedTokenSoNextCallReLogsIn()
    {
        // Requirement: a 401 on an in-flight request means the cached token
        // rotated/expired server-side. TvdbBaseClient clears its static Token
        // as a side effect (SendAuthorizedAsync) so the NEXT call re-logs-in,
        // even though this call itself still surfaces the 401 failure (401 is
        // not one of TvdbBaseClient's soft-fail statuses, so it throws here).
        TestApiKeyStore.Instance.TvdbKey = "test-tvdb-key";
        TvdbTokenAccess.Set(LoginBody("existing-token"));
        string firstPath = Unique("series");
        Handler.WhenGet(firstPath, MockResponse.Status(HttpStatusCode.Unauthorized));

        using TestableClient client = new();
        Func<Task<string?>> act = () => client.Get<string>(firstPath);
        await act.Should().ThrowAsync<HttpRequestException>();

        TvdbTokenAccess.Get().Should().BeNull();

        Handler.WhenPost("login", MockResponse.Json(HttpStatusCode.OK, LoginBody("fresh-token")));
        string secondPath = Unique("series");
        Handler.WhenGet(secondPath, MockResponse.Json(HttpStatusCode.OK, "\"ok\""));

        string? result = await client.Get<string>(secondPath);

        result.Should().Be("ok");
        Handler.Requests.Should().ContainSingle(r => r.Path.Contains("login"));
    }
}

/// <summary>
/// Reflection access to <see cref="TvdbBaseClient"/>'s <c>private static
/// TvdbLoginResponse? Token</c> field. It is process-wide and never reset by
/// production code (tokens live ~1 month by design), so tests that need to
/// control the login flow must reset/seed it directly — otherwise whichever
/// test in the assembly happens to run first "wins" the login for every test
/// after it.
/// </summary>
internal static class TvdbTokenAccess
{
    private static readonly PropertyInfo TokenProperty =
        typeof(TvdbBaseClient).GetProperty("Token", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("TvdbBaseClient.Token property not found.");

    public static void Reset() => TokenProperty.SetValue(null, null);

    public static void Set(TvdbLoginResponse token) => TokenProperty.SetValue(null, token);

    public static TvdbLoginResponse? Get() => (TvdbLoginResponse?)TokenProperty.GetValue(null);
}
