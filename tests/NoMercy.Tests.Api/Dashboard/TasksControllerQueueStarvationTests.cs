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
/// A busy priority band must not hide every other kind of work in the queue.
/// <para>
/// Capping the listing and then ordering it by priority meant the whole panel
/// came from the top band. An album import queues music at priority 5 and video
/// encodes sit at 4 and 0, so 13,779 music rows filled every slot and the
/// operator's video encodes — queued, real, and waiting — were not on the panel
/// at all. Bounding the listing is not licence to answer with one band.
/// </para>
/// </summary>
[Trait("Category", "Tasks")]
public class TasksControllerQueueStarvationTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private const int FloodPriority = 5;
    private const int BuriedPriority = 1;

    private readonly HttpClient _authed;
    private readonly List<int> _floodIds = [];
    private int _buriedId;

    public TasksControllerQueueStarvationTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
    }

    private static string MusicPayload(int index)
    {
        return $$"""
            {
              "$type": "NoMercy.MediaProcessing.Jobs.MediaJobs.MusicEncodeJob, NoMercy.MediaProcessing",
              "queueName": "encoder",
              "priority": 5,
              "libraryId": "01HQ5W4JJ9ZAX7721AQJZFQ7E1",
              "folderId": "01KXXNPYFBZF9ARJ8ZE2X6XCAP",
              "releaseId": "ec7f45e9-7935-45fb-83f0-5825c0181a2b",
              "trackId": "0000{{index:D4}}-1111-1111-1111-111111111111",
              "artistName": "Flood",
              "releaseName": "Album {{index}}",
              "inputFile": "Download/complete/Flood/{{index}}.flac"
            }
            """;
    }

    private static string VideoPayload()
    {
        return """
            {
              "$type": "NoMercy.MediaProcessing.Jobs.MediaJobs.VideoEncodeJob, NoMercy.MediaProcessing",
              "queueName": "encoder",
              "priority": 1,
              "status": "pending",
              "id": "770077",
              "folderId": "01HQ5W4Y1ZHYZKS87P0AG24ERE",
              "libraryId": "01HQ5W2HMZ5QKDSXTTN9EQRERH",
              "inputFile": "Download/complete/Show/Buried.mkv",
              "sourceDriverId": "01KQK5NFSRXDP6F83NZK1YA6WF"
            }
            """;
    }

    public async Task InitializeAsync()
    {
        await using QueueContext ctx = new();

        List<QueueJob> flood = [];
        for (int index = 0; index < UiLimits.MaximumTasksInList + 40; index++)
        {
            flood.Add(
                new()
                {
                    Queue = "encoder",
                    Priority = FloodPriority,
                    Payload = MusicPayload(index),
                    AvailableAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        QueueJob buried = new()
        {
            Queue = "encoder",
            Priority = BuriedPriority,
            Payload = VideoPayload(),
            AvailableAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        ctx.QueueJobs.AddRange(flood);
        ctx.QueueJobs.Add(buried);
        await ctx.SaveChangesAsync();

        _floodIds.AddRange(flood.Select(row => row.Id));
        _buriedId = buried.Id;
    }

    public async Task DisposeAsync()
    {
        await using QueueContext ctx = new();
        List<int> ids = [.. _floodIds, _buriedId];
        await ctx.QueueJobs.Where(job => ids.Contains(job.Id)).ExecuteDeleteAsync();
    }

    private async Task<JsonElement[]> GetQueueAsync()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/tasks/queue");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        JsonElement array =
            root.ValueKind == JsonValueKind.Object ? root.GetProperty("data") : root;

        return array.EnumerateArray().ToArray();
    }

    [Fact]
    public async Task ALowerPriorityEncode_IsStillListed_UnderAFloodOfHigherPriorityWork()
    {
        JsonElement[] rows = await GetQueueAsync();

        rows.Select(row => row.GetProperty("id").GetInt32())
            .Should()
            .Contain(
                _buriedId,
                "{0} rows at priority {1} must not take every slot — a video encode the "
                    + "operator queued is work in the queue, and a panel that cannot show it "
                    + "reports it as absent",
                _floodIds.Count,
                FloodPriority
            );
    }

    [Fact]
    public async Task TheListingStaysBounded_WhileCoveringMoreThanOneBand()
    {
        JsonElement[] rows = await GetQueueAsync();

        rows.Select(row => row.GetProperty("priority").GetInt32())
            .Distinct()
            .Count()
            .Should()
            .BeGreaterThan(1, "the point of the fix is that more than one band is represented");

        rows.Length.Should().BeLessThanOrEqualTo(UiLimits.MaximumTasksInList * 2);
    }
}
