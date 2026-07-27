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
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Queue;
using NoMercy.Encoder.Profiles;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// A queued card has no progress and no encoder chatter, so what the job is
/// going to do is the whole of what it can say. That answer comes from the
/// preset the job named for itself — not the folder's default, which is a
/// different preset whenever a job was dispatched against a specific one.
/// </summary>
[Trait("Category", "Tasks")]
public class TasksControllerQueuePlanTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _authed;

    private readonly Ulid _presetId = Ulid.NewUlid();
    private int _rowId;

    public TasksControllerQueuePlanTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
    }

    private string Payload()
    {
        return $$"""
            {
              "$type": "NoMercy.MediaProcessing.Jobs.MediaJobs.VideoEncodeJob, NoMercy.MediaProcessing",
              "queueName": "encoder",
              "priority": 4,
              "status": "pending",
              "forceFullReencode": false,
              "id": "990101",
              "folderId": "01HQ5W4Y1ZHYZKS87P0AG24ERE",
              "libraryId": "01HQ5W2HMZ5QKDSXTTN9EQRERH",
              "inputFile": "Download/complete/Show/S01E01.mkv",
              "sourceDriverId": "01KQK5NFSRXDP6F83NZK1YA6WF",
              "presetId": "{{_presetId}}"
            }
            """;
    }

    public async Task InitializeAsync()
    {
        EncodingProfile profile = BuiltinPresets
            .All()
            .Single(p => p.Name == BuiltinPresets.DefaultStreamingPresetName);

        await using MediaContext media = new();
        media.EncodingPresets.Add(
            new()
            {
                Id = _presetId,
                Name = PresetName,
                ProfileJson = JsonConvert.SerializeObject(profile),
                Source = "test",
            }
        );
        await media.SaveChangesAsync();

        await using QueueContext queue = new();
        QueueJob row = new()
        {
            Queue = "encoder",
            Priority = 4,
            Payload = Payload(),
            ReservedAt = null,
            AvailableAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        queue.QueueJobs.Add(row);
        await queue.SaveChangesAsync();

        _rowId = row.Id;
    }

    public async Task DisposeAsync()
    {
        await using QueueContext queue = new();
        await queue.QueueJobs.Where(j => j.Id == _rowId).ExecuteDeleteAsync();

        await using MediaContext media = new();
        await media.EncodingPresets.Where(p => p.Id == _presetId).ExecuteDeleteAsync();
    }

    private async Task<JsonElement> RowAsync()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/tasks/queue");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        JsonElement array =
            root.ValueKind == JsonValueKind.Object ? root.GetProperty("data") : root;

        JsonElement[] matches = array
            .EnumerateArray()
            .Where(row => row.GetProperty("id").GetInt32() == _rowId)
            .ToArray();

        matches.Should().ContainSingle("the seeded row must appear exactly once");
        return matches[0];
    }

    [Fact]
    public async Task AQueuedEncode_SaysWhatItIsGoingToProduce()
    {
        JsonElement plan = (await RowAsync()).GetProperty("plan");

        plan.ValueKind.Should().NotBe(JsonValueKind.Null, "the preset resolves, so a plan exists");
        plan.GetProperty("video").GetArrayLength().Should().BeGreaterThan(0);
        plan.GetProperty("audio").GetArrayLength().Should().BeGreaterThan(0);
        plan.GetProperty("video_mode").GetString().Should().Be("capped");
        plan.GetProperty("container").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TheProfileShown_IsThePresetTheJobNamed()
    {
        (await RowAsync()).GetProperty("profile").GetString().Should().Be(PresetName);
    }

    private const string PresetName = "Queue Plan Test Preset";
}
