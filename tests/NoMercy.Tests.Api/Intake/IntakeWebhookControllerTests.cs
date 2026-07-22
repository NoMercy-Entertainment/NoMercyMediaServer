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
[Trait(name: "Category", value: "Intake")]
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
            .WithWebHostBuilder(configuration: builder =>
            {
                builder.ConfigureTestServices(servicesConfiguration: services =>
                {
                    services.RemoveAll<IIntakeSettings>();
                    services.AddSingleton<IIntakeSettings>(implementationInstance: fakeSettings);
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
            entity: new()
            {
                Id = libraryId,
                Title = "Intake Inbox",
                Type = MediaTypes.InboxMediaType,
            }
        );

        context.Folders.Add(
            entity: new()
            {
                Id = folderId,
                Path = folderPath,
                DriverId = Driver.SystemLocalDriverId,
            }
        );

        context.FolderLibrary.Add(entity: new(folderId: folderId, libraryId: libraryId));

        context.SaveChanges();

        return (libraryId, folderPath);
    }

    // ── token gate ────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_NoTokenHeader_Returns401()
    {
        FakeIntakeSettings settings = new() { DropFolder = "/media/intake", Token = CorrectToken };
        HttpClient client = BuildClient(fakeSettings: settings);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/intake/webhook",
            value: new { path = "/media/intake/dropped.mkv" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_WrongToken_Returns401()
    {
        FakeIntakeSettings settings = new() { DropFolder = "/media/intake", Token = CorrectToken };
        HttpClient client = BuildClient(fakeSettings: settings);
        client.DefaultRequestHeaders.Add(name: TokenHeaderName, value: "wrong-token");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/intake/webhook",
            value: new { path = "/media/intake/dropped.mkv" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Unauthorized);
    }

    // ── happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_ValidTokenPathAndSeededInboxLibrary_Returns202AndPublishesFileCreatedEvent()
    {
        string dropFolder = $"/media/intake-{Guid.NewGuid():N}";
        (Ulid libraryId, string folderPath) = SeedInboxLibrary(folderPath: dropFolder);

        FakeIntakeSettings settings = new() { DropFolder = dropFolder, Token = CorrectToken };
        HttpClient client = BuildClient(fakeSettings: settings);
        client.DefaultRequestHeaders.Add(name: TokenHeaderName, value: CorrectToken);

        FileCreatedEvent? captured = null;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            handler: (@event, _) =>
            {
                if (@event.LibraryId == libraryId)
                    captured = @event;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/intake/webhook",
            value: new { path = $"{dropFolder}/dropped-file.mkv" }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected: HttpStatusCode.Accepted, because: body);

        captured.Should().NotBeNull(because: "the inbox pipeline must be re-triggered for the drop folder");
        captured!.LibraryId.Should().Be(expected: libraryId);
        captured.LibraryType.Should().Be(expected: MediaTypes.InboxMediaType);
        captured.FolderPath.Should().Be(expected: folderPath);
    }

    // ── path boundary check ─────────────────────────────────────────────

    [Fact]
    public async Task Webhook_PathTraversalOutsideDropFolder_Returns400AndPublishesNoEvent()
    {
        string dropFolder = $"/media/intake-{Guid.NewGuid():N}";
        (Ulid libraryId, _) = SeedInboxLibrary(folderPath: dropFolder);

        FakeIntakeSettings settings = new() { DropFolder = dropFolder, Token = CorrectToken };
        HttpClient client = BuildClient(fakeSettings: settings);
        client.DefaultRequestHeaders.Add(name: TokenHeaderName, value: CorrectToken);

        bool published = false;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            handler: (@event, _) =>
            {
                if (@event.LibraryId == libraryId)
                    published = true;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/intake/webhook",
            value: new { path = $"{dropFolder}/../etc/passwd" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
        published.Should().BeFalse(because: "a traversal path must never trigger the inbox pipeline");
    }

    // ── no drop folder configured ───────────────────────────────────────

    [Fact]
    public async Task Webhook_NoDropFolderConfigured_Returns409AndPublishesNoEvent()
    {
        FakeIntakeSettings settings = new() { DropFolder = null, Token = CorrectToken };
        HttpClient client = BuildClient(fakeSettings: settings);
        client.DefaultRequestHeaders.Add(name: TokenHeaderName, value: CorrectToken);

        bool published = false;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            handler: (_, _) =>
            {
                published = true;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/intake/webhook",
            value: new { path = "/media/intake/dropped.mkv" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Conflict);
        published
            .Should()
            .BeFalse(because: "no configured drop folder must never trigger the inbox pipeline");
    }

    // ── drop folder is a subfolder of an inbox library folder (exact-match only) ─

    [Fact]
    public async Task Webhook_DropFolderIsSubfolderOfInboxLibraryFolder_Returns409AndPublishesNoEvent()
    {
        string libraryFolder = $"/media/intake-{Guid.NewGuid():N}";
        string dropFolder = $"{libraryFolder}/incoming";
        (Ulid libraryId, _) = SeedInboxLibrary(folderPath: libraryFolder);

        FakeIntakeSettings settings = new() { DropFolder = dropFolder, Token = CorrectToken };
        HttpClient client = BuildClient(fakeSettings: settings);
        client.DefaultRequestHeaders.Add(name: TokenHeaderName, value: CorrectToken);

        bool published = false;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            handler: (_, _) =>
            {
                published = true;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/intake/webhook",
            value: new { path = $"{dropFolder}/dropped.mkv" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Conflict);
        published
            .Should()
            .BeFalse(
                because: "the drop folder must match an inbox library folder exactly; a subfolder must never trigger the inbox pipeline"
            );
    }

    // ── drop folder configured but not registered as an inbox library ───

    [Fact]
    public async Task Webhook_DropFolderNotRegisteredAsInboxLibrary_Returns409AndPublishesNoEvent()
    {
        string dropFolder = $"/media/unregistered-{Guid.NewGuid():N}";

        FakeIntakeSettings settings = new() { DropFolder = dropFolder, Token = CorrectToken };
        HttpClient client = BuildClient(fakeSettings: settings);
        client.DefaultRequestHeaders.Add(name: TokenHeaderName, value: CorrectToken);

        bool published = false;
        using IDisposable subscription = EventBusProvider.Current.Subscribe<FileCreatedEvent>(
            handler: (_, _) =>
            {
                published = true;
                return Task.CompletedTask;
            }
        );

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/intake/webhook",
            value: new { path = $"{dropFolder}/dropped.mkv" }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.Conflict);
        published
            .Should()
            .BeFalse(because: "an unregistered drop folder must never trigger the inbox pipeline");
    }

    // ── fake settings ────────────────────────────────────────────────────

    private sealed class FakeIntakeSettings : IIntakeSettings
    {
        public string? DropFolder { get; set; }

        public string? Token { get; set; }

        public Task<string?> GetDropFolderAsync(CancellationToken ct) =>
            Task.FromResult(result: DropFolder);

        public Task SetDropFolderAsync(string? path, CancellationToken ct)
        {
            DropFolder = path;
            return Task.CompletedTask;
        }

        public Task<bool> HasTokenAsync(CancellationToken ct) =>
            Task.FromResult(result: !string.IsNullOrEmpty(value: Token));

        public Task<string> IssueTokenAsync(CancellationToken ct)
        {
            Token = Guid.NewGuid().ToString(format: "N");
            return Task.FromResult(result: Token);
        }

        public Task<bool> VerifyTokenAsync(string? presented, CancellationToken ct) =>
            Task.FromResult(result: !string.IsNullOrEmpty(value: presented) && presented == Token);
    }
}
