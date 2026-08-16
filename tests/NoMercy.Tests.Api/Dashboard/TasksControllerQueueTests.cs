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
using NoMercy.Database.Models.Queue;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Covers the encoder-queue listing's liveness reporting. A job's scheduling
/// state lives on the queue row; the payload is the snapshot serialized when the
/// job was enqueued and is never rewritten, so its own "status" field stays
/// "pending" for the job's whole life.
/// </summary>
[Trait("Category", "Tasks")]
public class TasksControllerQueueTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _authed;

    private int _reservedRowId;
    private int _pendingRowId;
    private int _unparseableRowId;

    public TasksControllerQueueTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
    }

    // Verbatim shape of a real enqueued encode payload, taken from the live queue
    // database — including the "status":"pending" the dispatcher writes once and
    // never updates.
    private static string PayloadFor(string mediaId)
    {
        return $$"""
            {
              "$type": "NoMercy.MediaProcessing.Jobs.MediaJobs.VideoEncodeJob, NoMercy.MediaProcessing",
              "queueName": "encoder",
              "priority": 4,
              "status": "pending",
              "forceFullReencode": false,
              "id": "{{mediaId}}",
              "folderId": "01HQ5W4Y1ZHYZKS87P0AG24ERE",
              "libraryId": "01HQ5W2HMZ5QKDSXTTN9EQRERH",
              "inputFile": "Download/complete/Show/S01E01.mkv",
              "sourceDriverId": "01KQK5NFSRXDP6F83NZK1YA6WF"
            }
            """;
    }

    public async Task InitializeAsync()
    {
        await using QueueContext ctx = new();

        QueueJob reserved = new()
        {
            Queue = "encoder",
            Priority = 4,
            Payload = PayloadFor("990001"),
            ReservedAt = DateTime.UtcNow,
            AvailableAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        QueueJob pending = new()
        {
            Queue = "encoder",
            Priority = 4,
            Payload = PayloadFor("990002"),
            ReservedAt = null,
            AvailableAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        // Sorts ahead of both rows above (higher priority) and cannot be parsed,
        // so it is dropped from the parsed list while staying in the row list.
        // Anything that pairs a parsed job with a row by position rather than by
        // identity hands every later job this row's id and reservation.
        QueueJob unparseable = new()
        {
            Queue = "encoder",
            Priority = 9,
            Payload = "{ this is not valid json",
            ReservedAt = null,
            AvailableAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
        };

        ctx.QueueJobs.AddRange([unparseable, reserved, pending]);
        await ctx.SaveChangesAsync();

        _reservedRowId = reserved.Id;
        _pendingRowId = pending.Id;
        _unparseableRowId = unparseable.Id;
    }

    public async Task DisposeAsync()
    {
        await using QueueContext ctx = new();
        await ctx
            .QueueJobs.Where(j =>
                j.Id == _reservedRowId || j.Id == _pendingRowId || j.Id == _unparseableRowId
            )
            .ExecuteDeleteAsync();
    }

    private async Task<JsonElement[]> GetQueueAsync()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/tasks/queue");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string json = await response.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        JsonElement array =
            root.ValueKind == JsonValueKind.Object ? root.GetProperty("data") : root;

        return [.. array.EnumerateArray()];
    }

    private static JsonElement RowWithId(JsonElement[] rows, int id)
    {
        JsonElement[] matches = [.. rows.Where(r => r.GetProperty("id").GetInt32() == id)];
        matches.Should().ContainSingle("row {0} must appear exactly once in the queue listing", id);
        return matches[0];
    }

    [Fact]
    public async Task ReservedJob_IsReportedRunning_NotThePayloadsFrozenPendingStatus()
    {
        JsonElement[] rows = await GetQueueAsync();

        RowWithId(rows, _reservedRowId)
            .GetProperty("status")
            .GetString()
            .Should()
            .Be(
                "running",
                "the row carries ReservedAt, so the job is in flight — the payload's own "
                    + "\"status\" is a stale enqueue-time snapshot and must not be trusted"
            );
    }

    [Fact]
    public async Task UnreservedJob_IsReportedPending()
    {
        JsonElement[] rows = await GetQueueAsync();

        RowWithId(rows, _pendingRowId)
            .GetProperty("status")
            .GetString()
            .Should()
            .Be("pending", "the row has no ReservedAt, so the job has not started");
    }

    [Fact]
    public async Task EachRow_KeepsItsOwnPayload_WhenAnEarlierRowFailsToParse()
    {
        JsonElement[] rows = await GetQueueAsync();

        RowWithId(rows, _reservedRowId).GetProperty("payload_id").GetString().Should().Be("990001");
        RowWithId(rows, _pendingRowId).GetProperty("payload_id").GetString().Should().Be("990002");
    }

    [Fact]
    public async Task UnparseableRow_IsOmitted_RatherThanShiftingEveryLaterRow()
    {
        JsonElement[] rows = await GetQueueAsync();

        rows.Select(r => r.GetProperty("id").GetInt32())
            .Should()
            .NotContain(
                _unparseableRowId,
                "a row whose payload cannot be read describes no job and must be dropped"
            );

        // The reserved row must still be reported as reserved. If the parsed jobs
        // were zipped to rows by position, this job would have inherited the
        // dropped row's (unreserved) state and read "pending".
        RowWithId(rows, _reservedRowId).GetProperty("status").GetString().Should().Be("running");
    }
}
