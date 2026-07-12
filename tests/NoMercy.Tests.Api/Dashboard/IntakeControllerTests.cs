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
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.MediaProcessing.Intake;
using NoMercy.NmSystem.Domain;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Controller-level tests for the authenticated dashboard intake settings
/// endpoints (drop folder configuration + webhook token issuance). Separate
/// from IntakeWebhookControllerTests, which cover the anonymous
/// machine-to-machine webhook. Business logic (persistence, token hashing)
/// lives on IIntakeSettings and is substituted per test via
/// WithWebHostBuilder; these tests only exercise auth, the drop-folder /
/// Inbox-library validation, and the JSON envelope.
/// </summary>
[Trait("Category", "Intake")]
public class IntakeControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    public IntakeControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient BuildClient(FakeIntakeSettings fakeSettings)
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IIntakeSettings>();
                    services.AddSingleton<IIntakeSettings>(fakeSettings);
                });
            })
            .CreateClient();
    }

    private static string SeedInboxLibrary(string folderPath)
    {
        Ulid libraryId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();

        using MediaContext context = new();

        context.Libraries.Add(
            new()
            {
                Id = libraryId,
                Title = "Intake Inbox",
                Type = MediaTypes.InboxMediaType,
            }
        );

        context.Folders.Add(
            new()
            {
                Id = folderId,
                Path = folderPath,
                DriverId = Driver.SystemLocalDriverId,
            }
        );

        context.FolderLibrary.Add(new(folderId, libraryId));

        context.SaveChanges();

        return folderPath;
    }

    private static string SeedNonInboxLibrary(string folderPath)
    {
        Ulid libraryId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();

        using MediaContext context = new();

        context.Libraries.Add(
            new()
            {
                Id = libraryId,
                Title = "Some Movie Library",
                Type = MediaTypes.MovieMediaType,
            }
        );

        context.Folders.Add(
            new()
            {
                Id = folderId,
                Path = folderPath,
                DriverId = Driver.SystemLocalDriverId,
            }
        );

        context.FolderLibrary.Add(new(folderId, libraryId));

        context.SaveChanges();

        return folderPath;
    }

    // ── auth ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIndex_Unauthenticated_Returns401()
    {
        HttpClient client = BuildClient(new()).AsUnauthenticated();

        HttpResponseMessage response = await client.GetAsync("/api/v1/dashboard/intake");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutDropFolder_Unauthenticated_Returns401()
    {
        HttpClient client = BuildClient(new()).AsUnauthenticated();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/dashboard/intake/drop-folder",
            new { path = "/media/intake" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostToken_Unauthenticated_Returns401()
    {
        HttpClient client = BuildClient(new()).AsUnauthenticated();

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/intake/token",
            null
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET / ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIndex_Authenticated_ReturnsDropFolderHasTokenAndWebhookHints()
    {
        FakeIntakeSettings settings = new() { DropFolder = "/media/intake", Token = "abc" };
        HttpClient client = BuildClient(settings).AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync("/api/v1/dashboard/intake");

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("dropFolder").GetString().Should().Be("/media/intake");
        root.GetProperty("hasToken").GetBoolean().Should().BeTrue();
        root.GetProperty("webhookPath").GetString().Should().Be("api/v1/intake/webhook");
        root.GetProperty("webhookHeader").GetString().Should().Be("X-Intake-Token");

        root.TryGetProperty("token", out _).Should().BeFalse("the token must never be readable");
        root.TryGetProperty("tokenHash", out _)
            .Should()
            .BeFalse("the token hash must never be readable");
    }

    [Fact]
    public async Task GetIndex_NoDropFolderOrTokenConfigured_ReturnsNullAndFalse()
    {
        FakeIntakeSettings settings = new() { DropFolder = null, Token = null };
        HttpClient client = BuildClient(settings).AsAuthenticated();

        HttpResponseMessage response = await client.GetAsync("/api/v1/dashboard/intake");

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("dropFolder").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("hasToken").GetBoolean().Should().BeFalse();
    }

    // ── PUT /drop-folder — validation against Inbox libraries ────────────

    [Fact]
    public async Task PutDropFolder_PathNotAnInboxLibraryFolder_Returns400AndDoesNotPersist()
    {
        string nonInboxFolder = SeedNonInboxLibrary($"/media/movies-{Guid.NewGuid():N}");

        FakeIntakeSettings settings = new();
        HttpClient client = BuildClient(settings).AsAuthenticated();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/dashboard/intake/drop-folder",
            new { path = nonInboxFolder }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);

        settings.SetDropFolderCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task PutDropFolder_PathNotRegisteredAtAll_Returns400()
    {
        FakeIntakeSettings settings = new();
        HttpClient client = BuildClient(settings).AsAuthenticated();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/dashboard/intake/drop-folder",
            new { path = $"/media/unregistered-{Guid.NewGuid():N}" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        settings.SetDropFolderCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task PutDropFolder_ValidInboxLibraryFolder_Returns200AndInvokesSetDropFolder()
    {
        string inboxFolder = SeedInboxLibrary($"/media/intake-{Guid.NewGuid():N}");

        FakeIntakeSettings settings = new();
        HttpClient client = BuildClient(settings).AsAuthenticated();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/dashboard/intake/drop-folder",
            new { path = inboxFolder }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("dropFolder").GetString().Should().Be(inboxFolder);

        settings.SetDropFolderCalls.Should().ContainSingle().Which.Should().Be(inboxFolder);
        settings.DropFolder.Should().Be(inboxFolder);
    }

    [Fact]
    public async Task PutDropFolder_TrailingSlashAndBackslashVariants_StillMatchInboxLibraryFolder()
    {
        string inboxFolder = SeedInboxLibrary($"/media/intake-{Guid.NewGuid():N}");

        FakeIntakeSettings settings = new();
        HttpClient client = BuildClient(settings).AsAuthenticated();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/dashboard/intake/drop-folder",
            new { path = inboxFolder + "/" }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        settings.SetDropFolderCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task PutDropFolder_EmptyPath_ClearsWithoutLibraryValidation()
    {
        FakeIntakeSettings settings = new() { DropFolder = "/media/old-intake" };
        HttpClient client = BuildClient(settings).AsAuthenticated();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/dashboard/intake/drop-folder",
            new { path = "" }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("dropFolder").ValueKind.Should().Be(JsonValueKind.Null);

        settings.SetDropFolderCalls.Should().ContainSingle().Which.Should().BeNull();
        settings.DropFolder.Should().BeNull();
    }

    // ── POST /token ────────────────────────────────────────────────────────

    [Fact]
    public async Task PostToken_ReturnsNonEmptyPlaintextTokenOnly()
    {
        FakeIntakeSettings settings = new();
        HttpClient client = BuildClient(settings).AsAuthenticated();

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/dashboard/intake/token",
            null
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        string? token = root.GetProperty("token").GetString();
        token.Should().NotBeNullOrEmpty();
        token.Should().Be(settings.Token, "the plaintext returned must be the one just issued");

        List<string> propertyNames = root.EnumerateObject().Select(p => p.Name).ToList();
        propertyNames.Should().Equal(["token"], "no hash or other secret material may be present");
    }

    [Fact]
    public async Task PostToken_CalledTwice_IssuesADifferentTokenEachTime()
    {
        FakeIntakeSettings settings = new();
        HttpClient client = BuildClient(settings).AsAuthenticated();

        HttpResponseMessage firstResponse = await client.PostAsync(
            "/api/v1/dashboard/intake/token",
            null
        );
        HttpResponseMessage secondResponse = await client.PostAsync(
            "/api/v1/dashboard/intake/token",
            null
        );

        using JsonDocument firstDoc = JsonDocument.Parse(
            await firstResponse.Content.ReadAsStringAsync()
        );
        using JsonDocument secondDoc = JsonDocument.Parse(
            await secondResponse.Content.ReadAsStringAsync()
        );

        string firstToken = firstDoc.RootElement.GetProperty("token").GetString()!;
        string secondToken = secondDoc.RootElement.GetProperty("token").GetString()!;

        firstToken.Should().NotBe(secondToken, "issuing a token rotates the stored hash");
    }

    // ── fake settings ────────────────────────────────────────────────────

    private sealed class FakeIntakeSettings : IIntakeSettings
    {
        public string? DropFolder { get; set; }

        public string? Token { get; set; }

        public List<string?> SetDropFolderCalls { get; } = [];

        public Task<string?> GetDropFolderAsync(CancellationToken ct) =>
            Task.FromResult(DropFolder);

        public Task SetDropFolderAsync(string? path, CancellationToken ct)
        {
            SetDropFolderCalls.Add(path);
            DropFolder = path;
            return Task.CompletedTask;
        }

        public Task<bool> HasTokenAsync(CancellationToken ct) =>
            Task.FromResult(!string.IsNullOrEmpty(Token));

        public Task<string> IssueTokenAsync(CancellationToken ct)
        {
            Token = Guid.NewGuid().ToString("N");
            return Task.FromResult(Token);
        }

        public Task<bool> VerifyTokenAsync(string? presented, CancellationToken ct) =>
            Task.FromResult(!string.IsNullOrEmpty(presented) && presented == Token);
    }
}
