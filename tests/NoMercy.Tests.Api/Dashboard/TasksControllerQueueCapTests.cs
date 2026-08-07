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
using NoMercy.NmSystem.Domain;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// Covers the bound on the encoder-queue listing.
/// <para>
/// This endpoint is polled, and an unbounded sort has to buffer every matching
/// row — payload included — to order it. Music encode payloads run to a megabyte
/// each, so a deep queue asked SQLite to spill gigabytes of temp and the poll
/// died on "database or disk is full": the panel went blank exactly when there
/// was most to show.
/// </para>
/// </summary>
[Trait("Category", "Tasks")]
public class TasksControllerQueueCapTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private const int Overflow = 20;

    private readonly HttpClient _authed;
    private readonly List<int> _rowIds = [];

    public TasksControllerQueueCapTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
    }

    private static string PayloadFor(int mediaId)
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

        List<QueueJob> rows = [];
        for (int index = 0; index < UiLimits.MaximumTasksInList + Overflow; index++)
        {
            rows.Add(
                new()
                {
                    Queue = "encoder",
                    Priority = 4,
                    Payload = PayloadFor(880000 + index),
                    ReservedAt = null,
                    AvailableAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        ctx.QueueJobs.AddRange(rows);
        await ctx.SaveChangesAsync();

        _rowIds.AddRange(rows.Select(row => row.Id));
    }

    public async Task DisposeAsync()
    {
        await using QueueContext ctx = new();
        await ctx.QueueJobs.Where(j => _rowIds.Contains(j.Id)).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task QueueListing_IsCapped_NoMatterHowDeepTheQueueIs()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/tasks/queue");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string json = await response.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        JsonElement array =
            root.ValueKind == JsonValueKind.Object ? root.GetProperty("data") : root;

        array
            .EnumerateArray()
            .Count()
            .Should()
            .BeLessThanOrEqualTo(
                UiLimits.MaximumTasksInList,
                "the listing must stay bounded — {0} rows were queued, and ordering all of "
                    + "them spills their payloads to temp",
                UiLimits.MaximumTasksInList + Overflow
            );
    }
}
