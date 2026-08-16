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
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

// Locks the current contract of LogController: the class carries
// [Authorize(Policy = "Moderator")], matching every other Dashboard/Admin
// controller in this suite. A merely-authenticated, non-moderator user (the
// seeded SecondaryUserId: Allowed=true, Owner=false, Manage=false) is rejected
// with a 403; the default test identity (Owner+Manage, which satisfies
// "Moderator") still gets a 200.
[Trait("Category", "DashboardLogs")]
public class LogControllerTests : IClassFixture<NoMercyApiFactory>
{
    private const string BaseUrl = "/api/v1/dashboard/logs";

    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;
    private readonly HttpClient _secondaryUser;

    public LogControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
        _secondaryUser = factory.CreateClient().AsSecondaryUser();
    }

    [Fact]
    public async Task GetLogs_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(BaseUrl);

        response
            .StatusCode.Should()
            .BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetLogs_ReturnsOkWithDataArray_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync(BaseUrl);
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("data", out JsonElement data).Should().BeTrue();
        data.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetLogs_ReturnsForbidden_WhenSecondaryUserNonModerator()
    {
        // LogController requires the "Moderator" policy: a merely-authenticated,
        // non-moderator user (Allowed=true, Owner=false, Manage=false) is
        // rejected — server logs are not readable by any authenticated user.
        HttpResponseMessage response = await _secondaryUser.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLogs_ReturnsEmptyData_WhenLimitIsZero()
    {
        HttpResponseMessage response = await _authed.GetAsync($"{BaseUrl}?limit=0");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetLogs_ReturnsEmptyData_WhenTypesFilterMatchesNothing()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"{BaseUrl}?types=contract-test-nonexistent-type-zzz"
        );
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(0, "no real log entry can match a garbage type filter");
    }

    [Fact]
    public async Task GetLogs_ReturnsEmptyData_WhenLevelsFilterMatchesNothing()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"{BaseUrl}?levels=contract-test-nonexistent-level-zzz"
        );
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(0, "no real log entry can match a garbage level filter");
    }

    [Fact]
    public async Task GetLogs_ReturnsEmptyData_WhenMessageFilterMatchesNothing()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"{BaseUrl}?filter=contract-test-nonexistent-message-substring-zzz"
        );
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetArrayLength()
            .Should()
            .Be(0, "no real log entry can match a garbage message substring");
    }

    [Fact]
    public async Task GetLogLevels_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync($"{BaseUrl}/levels");

        response
            .StatusCode.Should()
            .BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetLogLevels_ReturnsForbidden_WhenSecondaryUserNonModerator()
    {
        HttpResponseMessage response = await _secondaryUser.GetAsync($"{BaseUrl}/levels");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLogLevels_ReturnsAllSixSerilogLevelsInOrder_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync($"{BaseUrl}/levels");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");
        data.ValueKind.Should().Be(JsonValueKind.Array);

        string[] levels = [.. data.EnumerateArray().Select(e => e.GetString()!)];

        levels.Should().Equal(["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"]);
    }

    [Fact]
    public async Task GetLogTypes_ReturnsUnauthorized_WhenAnonymous()
    {
        HttpResponseMessage response = await _unauthed.GetAsync($"{BaseUrl}/types");

        response
            .StatusCode.Should()
            .BeOneOf([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    [Fact]
    public async Task GetLogTypes_ReturnsForbidden_WhenSecondaryUserNonModerator()
    {
        HttpResponseMessage response = await _secondaryUser.GetAsync($"{BaseUrl}/types");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLogTypes_ReturnsKnownTypeCatalogueEntries_WhenAuthenticated()
    {
        HttpResponseMessage response = await _authed.GetAsync($"{BaseUrl}/types");
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");
        data.ValueKind.Should().Be(JsonValueKind.Array);
        data.GetArrayLength().Should().BeGreaterThan(0);

        JsonElement appType = data.EnumerateArray()
            .First(e => e.GetProperty("name").GetString() == "app");

        appType.GetProperty("display_name").GetString().Should().Be("App");
        appType.GetProperty("type").GetString().Should().Be("System");
        appType.TryGetProperty("colorHex", out _).Should().BeTrue();
    }
}
