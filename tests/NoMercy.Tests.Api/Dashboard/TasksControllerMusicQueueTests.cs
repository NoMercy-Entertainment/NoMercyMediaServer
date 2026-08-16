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
/// A queued music encode has to appear in the encoder panel.
/// <para>
/// It never did. The listing only recognised a payload it could read as a
/// VideoEncodeJob, and a music encode is not one, so every music row was
/// dropped from the encode list and from the maintenance list alike. Video rows
/// further down the queue filled the panel and hid it — until an album import
/// put 14,849 music encodes at priority 5, above every video encode, and the
/// panel went blank while the server worked through them.
/// </para>
/// </summary>
[Trait("Category", "Tasks")]
public class TasksControllerMusicQueueTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private const string TrackId = "11111111-1111-1111-1111-111111111111";
    private const string ReleaseId = "ec7f45e9-7935-45fb-83f0-5825c0181a2b";

    private readonly HttpClient _authed;
    private int _rowId;

    public TasksControllerMusicQueueTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
    }

    // The shape the dispatcher writes now: ids and the destination, no release.
    private static string MusicPayload()
    {
        return $$"""
            {
              "$type": "NoMercy.MediaProcessing.Jobs.MediaJobs.MusicEncodeJob, NoMercy.MediaProcessing",
              "queueName": "encoder",
              "priority": 5,
              "status": "pending",
              "libraryId": "01HQ5W4JJ9ZAX7721AQJZFQ7E1",
              "folderId": "01KXXNPYFBZF9ARJ8ZE2X6XCAP",
              "id": "{{ReleaseId}}",
              "releaseId": "{{ReleaseId}}",
              "trackId": "{{TrackId}}",
              "basePath": "Music/Eagles/Eagles",
              "artistName": "Eagles",
              "releaseName": "Eagles",
              "year": 1972,
              "inputFolder": "Download/complete/Eagles",
              "inputFile": "Download/complete/Eagles/03 Chug All Night.flac"
            }
            """;
    }

    public async Task InitializeAsync()
    {
        await using QueueContext ctx = new();

        QueueJob row = new()
        {
            Queue = "encoder-cpu",
            // Above the video encodes, which is exactly how it ended up alone at
            // the top of the listing.
            Priority = 5,
            Payload = MusicPayload(),
            ReservedAt = null,
            AvailableAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        ctx.QueueJobs.Add(row);
        await ctx.SaveChangesAsync();

        _rowId = row.Id;
    }

    public async Task DisposeAsync()
    {
        await using QueueContext ctx = new();
        await ctx.QueueJobs.Where(job => job.Id == _rowId).ExecuteDeleteAsync();
    }

    private async Task<JsonElement[]> GetQueueAsync()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/tasks/queue");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        JsonElement array =
            root.ValueKind == JsonValueKind.Object ? root.GetProperty("data") : root;

        return [.. array.EnumerateArray()];
    }

    [Fact]
    public async Task AQueuedMusicEncode_AppearsInTheEncoderPanel()
    {
        JsonElement[] rows = await GetQueueAsync();

        rows.Select(row => row.GetProperty("id").GetInt32())
            .Should()
            .Contain(
                _rowId,
                "a music encode is queued work the operator is waiting on — a panel "
                    + "that drops it reports the server as idle while it encodes an album"
            );
    }

    [Fact]
    public async Task TheMusicCard_NamesTheReleaseItIsEncoding()
    {
        JsonElement[] rows = await GetQueueAsync();

        JsonElement card = rows.Single(row => row.GetProperty("id").GetInt32() == _rowId);

        card.GetProperty("title")
            .GetString()
            .Should()
            .Contain("Eagles", "a card the operator cannot identify is not worth drawing");

        card.GetProperty("status").GetString().Should().Be("pending");
    }
}
