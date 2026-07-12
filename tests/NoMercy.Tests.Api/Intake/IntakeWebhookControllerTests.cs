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
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.Events;
using NoMercy.Events.FileWatcher;
using NoMercy.MediaProcessing.Intake;
using NoMercy.NmSystem.Domain;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Intake;

/// <summary>
/// Controller-level tests for the authenticated inbound intake webhook. The
/// token check is a trust boundary — a render client (or any authorized
/// producer) posts the path of a file it dropped into the configured drop
/// folder, and the server re-triggers the Inbox pipeline by publishing a
/// FileCreatedEvent. Business logic (drop-folder config, token hashing)
/// lives on IIntakeSettings and is substituted per test via
/// WithWebHostBuilder; these tests only exercise the token gate, the path
/// boundary check, the drop-folder/library resolution, and the resulting
/// HTTP status + event publication.
/// </summary>
[Trait("Category", "Intake")]
public class IntakeWebhookControllerTests : IClassFixture<NoMercyApiFactory>
{
    private const string TokenHeaderName = "X-Intake-Token";
    private const string CorrectToken = "correct-intake-token";

    private readonly NoMercyApiFactory _factory;

    public IntakeWebhookControllerTests(NoMercyApiFactory factory)
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

    private static (Ulid LibraryId, string FolderPath) SeedInboxLibrary(string folderPath)
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

        return (libraryId, folderPath);
    }

    // ── token gate ────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_NoTokenHeader_Returns401()
    {
        FakeIntakeSettings settings = new() { DropFolder = "/media/intake", Token = CorrectToken };
        HttpClient client = BuildClient(settings);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/intake/webhook",
            new { path = "/media/intake/dropped.mkv" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_WrongToken_Returns401()
    {
        FakeIntakeSettings settings = new() { DropFolder = "/media/intake", Token = CorrectToken };
        HttpClient client = BuildClient(settings);
        client.DefaultRequestHeaders.Add(TokenHeaderName, "wrong-token");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/intake/webhook",
            new { path = "/media/intake/dropped.mkv" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_ValidTokenPathAndSeededInboxLibrary_Returns202AndPublishesFileCreatedEvent()
    {
        string dropFolder = $"/media/intake-{Guid.NewGuid():N}";
        (Ulid libraryId, string folderPath) = SeedInboxLibrary(dropFolder);

        FakeIntakeSettings settings = new() { DropFolder = dropFolder, Token = CorrectToken };
        HttpClient client = BuildClient(settings);
        client.DefaultRequestHeaders.Add(TokenHeaderName, CorrectToken);

        FileCreatedEvent? captured = null;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            (@event, _) =>
            {
                if (@event.LibraryId == libraryId)
                    captured = @event;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/intake/webhook",
            new { path = $"{dropFolder}/dropped-file.mkv" }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, body);

        captured.Should().NotBeNull("the inbox pipeline must be re-triggered for the drop folder");
        captured!.LibraryId.Should().Be(libraryId);
        captured.LibraryType.Should().Be(MediaTypes.InboxMediaType);
        captured.FolderPath.Should().Be(folderPath);
    }

    // ── path boundary check ─────────────────────────────────────────────

    [Fact]
    public async Task Webhook_PathTraversalOutsideDropFolder_Returns400AndPublishesNoEvent()
    {
        string dropFolder = $"/media/intake-{Guid.NewGuid():N}";
        (Ulid libraryId, _) = SeedInboxLibrary(dropFolder);

        FakeIntakeSettings settings = new() { DropFolder = dropFolder, Token = CorrectToken };
        HttpClient client = BuildClient(settings);
        client.DefaultRequestHeaders.Add(TokenHeaderName, CorrectToken);

        bool published = false;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            (@event, _) =>
            {
                if (@event.LibraryId == libraryId)
                    published = true;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/intake/webhook",
            new { path = $"{dropFolder}/../etc/passwd" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        published.Should().BeFalse("a traversal path must never trigger the inbox pipeline");
    }

    // ── no drop folder configured ───────────────────────────────────────

    [Fact]
    public async Task Webhook_NoDropFolderConfigured_Returns409AndPublishesNoEvent()
    {
        FakeIntakeSettings settings = new() { DropFolder = null, Token = CorrectToken };
        HttpClient client = BuildClient(settings);
        client.DefaultRequestHeaders.Add(TokenHeaderName, CorrectToken);

        bool published = false;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            (_, _) =>
            {
                published = true;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/intake/webhook",
            new { path = "/media/intake/dropped.mkv" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        published
            .Should()
            .BeFalse("no configured drop folder must never trigger the inbox pipeline");
    }

    // ── drop folder is a subfolder of an inbox library folder (exact-match only) ─

    [Fact]
    public async Task Webhook_DropFolderIsSubfolderOfInboxLibraryFolder_Returns409AndPublishesNoEvent()
    {
        string libraryFolder = $"/media/intake-{Guid.NewGuid():N}";
        string dropFolder = $"{libraryFolder}/incoming";
        (Ulid libraryId, _) = SeedInboxLibrary(libraryFolder);

        FakeIntakeSettings settings = new() { DropFolder = dropFolder, Token = CorrectToken };
        HttpClient client = BuildClient(settings);
        client.DefaultRequestHeaders.Add(TokenHeaderName, CorrectToken);

        bool published = false;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            (_, _) =>
            {
                published = true;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/intake/webhook",
            new { path = $"{dropFolder}/dropped.mkv" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        published
            .Should()
            .BeFalse(
                "the drop folder must match an inbox library folder exactly; a subfolder must never trigger the inbox pipeline"
            );
    }

    // ── drop folder configured but not registered as an inbox library ───

    [Fact]
    public async Task Webhook_DropFolderNotRegisteredAsInboxLibrary_Returns409AndPublishesNoEvent()
    {
        string dropFolder = $"/media/unregistered-{Guid.NewGuid():N}";

        FakeIntakeSettings settings = new() { DropFolder = dropFolder, Token = CorrectToken };
        HttpClient client = BuildClient(settings);
        client.DefaultRequestHeaders.Add(TokenHeaderName, CorrectToken);

        bool published = false;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            (_, _) =>
            {
                published = true;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/intake/webhook",
            new { path = $"{dropFolder}/dropped.mkv" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        published
            .Should()
            .BeFalse("an unregistered drop folder must never trigger the inbox pipeline");
    }

    // ── fake settings ────────────────────────────────────────────────────

    private sealed class FakeIntakeSettings : IIntakeSettings
    {
        public string? DropFolder { get; set; }

        public string? Token { get; set; }

        public Task<string?> GetDropFolderAsync(CancellationToken ct) =>
            Task.FromResult(DropFolder);

        public Task SetDropFolderAsync(string? path, CancellationToken ct)
        {
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
