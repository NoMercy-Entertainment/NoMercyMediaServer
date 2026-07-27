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
using NoMercy.Database.Models.Storage;
using NoMercy.Encoder.Profiles;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// A queued card has no progress and no encoder chatter, so what the job is
/// going to do is the whole of what it can say.
///
/// <para>The answer is every preset linked to the job's folder, which is the
/// rule <c>VideoEncodeJob</c> runs by: a folder carrying two presets produces
/// both in one coordinated encode. Naming one of them — the folder's default,
/// or whichever link came back first — described half the work.</para>
/// </summary>
[Trait("Category", "Tasks")]
public class TasksControllerQueuePlanTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _authed;

    private readonly Ulid _folderId = Ulid.NewUlid();
    private readonly Ulid _streamingPresetId = Ulid.NewUlid();
    private readonly Ulid _archivePresetId = Ulid.NewUlid();
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
              "folderId": "{{_folderId}}",
              "libraryId": "01HQ5W2HMZ5QKDSXTTN9EQRERH",
              "inputFile": "Download/complete/Show/S01E01.mkv",
              "sourceDriverId": "01KQK5NFSRXDP6F83NZK1YA6WF"
            }
            """;
    }

    private static string ProfileJsonFor(string builtinName)
    {
        return JsonConvert.SerializeObject(
            BuiltinPresets.All().Single(profile => profile.Name == builtinName)
        );
    }

    public async Task InitializeAsync()
    {
        await using MediaContext media = new();

        media.EncodingPresets.AddRange(
            new EncodingPreset
            {
                Id = _streamingPresetId,
                Name = StreamingPresetName,
                ProfileJson = ProfileJsonFor(BuiltinPresets.DefaultStreamingPresetName),
                Source = "test",
            },
            new EncodingPreset
            {
                Id = _archivePresetId,
                Name = ArchivePresetName,
                ProfileJson = ProfileJsonFor(ArchiveBuiltinName),
                Source = "test",
            }
        );

        // Both linked to the same folder, neither marked default — which is what
        // a real library looks like, and why IsDefault cannot be the selector.
        media.Folders.Add(
            new()
            {
                Id = _folderId,
                DriverId = Driver.SystemLocalDriverId,
                Path = $"/tmp/queue-plan-tests/{_folderId}",
                EncodingPresetFolders =
                [
                    new() { PresetId = _streamingPresetId, FolderId = _folderId },
                    new() { PresetId = _archivePresetId, FolderId = _folderId },
                ],
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
        await media
            .EncodingPresetFolders.Where(link => link.FolderId == _folderId)
            .ExecuteDeleteAsync();
        await media.Folders.Where(folder => folder.Id == _folderId).ExecuteDeleteAsync();
        await media
            .EncodingPresets.Where(preset =>
                preset.Id == _streamingPresetId || preset.Id == _archivePresetId
            )
            .ExecuteDeleteAsync();
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

        plan.ValueKind.Should().NotBe(JsonValueKind.Null, "the presets resolve, so a plan exists");
        plan.GetProperty("video").GetArrayLength().Should().BeGreaterThan(0);
        plan.GetProperty("audio").GetArrayLength().Should().BeGreaterThan(0);
        plan.GetProperty("container").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TwoPresetsOnAFolder_AreOneMergedAnswer_NotWhicheverCameBackFirst()
    {
        JsonElement row = await RowAsync();

        // The archive preset encodes at the source's own height and the
        // streaming one runs a ladder, so a merge that dropped either loses a
        // codec the encode is going to write.
        string[] codecs = row.GetProperty("plan")
            .GetProperty("video")
            .EnumerateArray()
            .Select(rendition => rendition.GetProperty("codec").GetString()!)
            .Distinct()
            .ToArray();

        codecs.Should().Contain("H264", "the streaming preset's ladder is H.264");
        codecs.Should().Contain("H265", "the archive preset writes HEVC");

        row.GetProperty("plan")
            .GetProperty("video_mode")
            .GetString()
            .Should()
            .Be("capped", "one of the two is an auto ladder, so the list is a ceiling");
    }

    [Fact]
    public async Task TheProfileShown_NamesEveryPresetTheEncodeWillRun()
    {
        string? profile = (await RowAsync()).GetProperty("profile").GetString();

        profile.Should().Contain(StreamingPresetName);
        profile.Should().Contain(ArchivePresetName);
    }

    private const string ArchiveBuiltinName = "HEVC Archive (Visually Lossless)";
    private const string StreamingPresetName = "Queue Plan Streaming Preset";
    private const string ArchivePresetName = "Queue Plan Archive Preset";
}
