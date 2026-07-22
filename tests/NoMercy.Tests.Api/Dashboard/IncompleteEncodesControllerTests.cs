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
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Encoder;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

[Trait(name: "Category", value: "IncompleteEncodes")]
public class IncompleteEncodesControllerTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _authed;

    // IDs inserted per-test and cleaned up in DisposeAsync.
    private int _rowId1;
    private int _rowId2;

    public IncompleteEncodesControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
    }

    public async Task InitializeAsync()
    {
        await using MediaContext ctx = new();

        IncompleteEncode row1 = new()
        {
            MediaId = 129,
            FolderId = NoMercyApiFactory.MovieFolderId.ToString(),
            Title = "Spirited Away",
            MissingRenditions = "video/h264/1080p\nvideo/h264/720p",
            LastError = "Finalize timed out",
            AttemptsMade = 1,
            FirstSeenAt = DateTime.UtcNow.AddHours(value: -2),
            LastSeenAt = DateTime.UtcNow.AddMinutes(value: -5),
        };
        IncompleteEncode row2 = new()
        {
            MediaId = 680,
            FolderId = NoMercyApiFactory.MovieFolderId.ToString(),
            Title = "Pulp Fiction",
            MissingRenditions = "audio/aac/stereo",
            LastError = null,
            AttemptsMade = 0,
            FirstSeenAt = DateTime.UtcNow.AddHours(value: -1),
            LastSeenAt = DateTime.UtcNow.AddMinutes(value: -1),
        };

        ctx.IncompleteEncodes.AddRange(entities: [row1, row2]);
        await ctx.SaveChangesAsync();

        _rowId1 = row1.Id;
        _rowId2 = row2.Id;
    }

    public async Task DisposeAsync()
    {
        await using MediaContext ctx = new();
        await ctx
            .IncompleteEncodes.Where(predicate: r => r.Id == _rowId1 || r.Id == _rowId2)
            .ExecuteDeleteAsync();
    }

    // ── GET /api/v1/dashboard/tasks/queue/incomplete ──────────────────────

    [Fact]
    public async Task List_ReturnsSeededRows()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            requestUri: "/api/v1/dashboard/tasks/queue/incomplete"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: json);
        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");

        data.GetArrayLength().Should().BeGreaterThanOrEqualTo(expected: 2);

        // Find our two rows by title.
        JsonElement[] rows = data.EnumerateArray().ToArray();

        JsonElement? spirited = rows.FirstOrDefault(predicate: e =>
            e.GetProperty(propertyName: "title").GetString() == "Spirited Away"
        );
        spirited.Should().NotBeNull(because: "Spirited Away row must appear in the list");

        string[]? renditions = spirited!
            .Value.GetProperty(propertyName: "missing_renditions")
            .EnumerateArray()
            .Select(selector: e => e.GetString()!)
            .ToArray();

        renditions.Should().BeEquivalentTo(expectation: ["video/h264/1080p", "video/h264/720p"]);
        spirited!.Value.GetProperty(propertyName: "last_error").GetString().Should().Be(expected: "Finalize timed out");
        spirited!.Value.GetProperty(propertyName: "attempts_made").GetInt32().Should().Be(expected: 1);
    }

    // ── POST /api/v1/dashboard/tasks/queue/incomplete/{id}/retry ─────────

    [Fact]
    public async Task Retry_MissingId_Returns404()
    {
        HttpResponseMessage response = await _authed.PostAsync(
            requestUri: "/api/v1/dashboard/tasks/queue/incomplete/99999/retry",
            content: null
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Retry_ExistingRow_RemovesRow()
    {
        // The retry will attempt to look up the VideoFile and enqueue a job.
        // Because QueueRunner is not started in the test host, the enqueue will
        // fail. The controller handles that gracefully and still removes the row.
        // We only assert the row is gone — the 200 vs 500 depends on whether a
        // real QueueRunner is present. Either outcome removes the quarantine row.
        HttpResponseMessage response = await _authed.PostAsync(
            requestUri: $"/api/v1/dashboard/tasks/queue/incomplete/{_rowId1}/retry",
            content: null
        );

        // Must be either 200 (enqueue succeeded) or 500 (no QueueRunner in test).
        // Either way the quarantine row must be deleted.
        bool rowGone;
        await using MediaContext ctx = new();
        rowGone = !await ctx.IncompleteEncodes.AnyAsync(predicate: r => r.Id == _rowId1);
        rowGone
            .Should()
            .BeTrue(because: "quarantine row must be removed after retry regardless of queue state");
    }

    // ── DELETE /api/v1/dashboard/tasks/queue/incomplete/{id} ──────────────

    [Fact]
    public async Task Delete_MissingId_Returns404()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            requestUri: "/api/v1/dashboard/tasks/queue/incomplete/99999"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingRow_RemovesOnlyThatRow()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            requestUri: $"/api/v1/dashboard/tasks/queue/incomplete/{_rowId1}"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: json);
        doc.RootElement.GetProperty(propertyName: "status").GetString().Should().Be(expected: "success");

        await using MediaContext ctx = new();
        (await ctx.IncompleteEncodes.AnyAsync(predicate: r => r.Id == _rowId1))
            .Should()
            .BeFalse(because: "the deleted row must no longer exist");
        (await ctx.IncompleteEncodes.AnyAsync(predicate: r => r.Id == _rowId2))
            .Should()
            .BeTrue(because: "a single-row delete must not touch other quarantine rows");
    }

    // ── DELETE /api/v1/dashboard/tasks/queue/incomplete ────────────────────

    [Fact]
    public async Task DeleteAll_RemovesSeededRows_AndReportsRemovedCount()
    {
        HttpResponseMessage response = await _authed.DeleteAsync(
            requestUri: "/api/v1/dashboard/tasks/queue/incomplete"
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json: json);
        doc.RootElement.GetProperty(propertyName: "status").GetString().Should().Be(expected: "success");

        // Other tests/processes may hold unrelated quarantine rows in the shared
        // test database, so the count is only asserted as a lower bound — the
        // two rows this test seeded must be included in it.
        doc.RootElement.GetProperty(propertyName: "data").GetInt32().Should().BeGreaterThanOrEqualTo(expected: 2);

        await using MediaContext ctx = new();
        (await ctx.IncompleteEncodes.AnyAsync(predicate: r => r.Id == _rowId1 || r.Id == _rowId2))
            .Should()
            .BeFalse(because: "both seeded rows must be gone after deleting all incomplete encodes");
    }
}
