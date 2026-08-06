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

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using NoMercy.Database;
using NoMercy.Database.Models.Queue;
using NoMercy.Queue.MediaServer;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// Covers the pass that rewrites job payloads which carried their input inline.
/// <para>
/// The stake is a queued backlog: a music encode used to serialize the entire
/// MusicBrainz release into every track's payload, so the same megabyte was
/// stored once per track. Compacting that has to move the release out without
/// losing which track each row was for — a row that forgets its own track is a
/// queued encode silently dropped.
/// </para>
/// </summary>
[Trait("Category", "Queue")]
public class QueuePayloadCompactionTests : IAsyncLifetime
{
    private static readonly Guid ReleaseId = new("ec7f45e9-7935-45fb-83f0-5825c0181a2b");
    private static readonly Guid TrackOneId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TrackTwoId = new("22222222-2222-2222-2222-222222222222");

    private SqliteConnection _connection = null!;
    private IDbContextFactory<QueueContext> _factory = null!;

    public async Task InitializeAsync()
    {
        _connection = new("DataSource=:memory:");
        await _connection.OpenAsync();

        _factory = new TestContextFactory(_connection);

        await using QueueContext context = await _factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// A payload in the shape the old dispatcher wrote: the whole release nested
    /// under folderMetaData, with the track duplicated alongside it.
    /// </summary>
    private static string LegacyPayload(Guid trackId, string trackTitle)
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
              "inputFolder": "Download/complete/Album",
              "inputFile": "Download/complete/Album/01 {{trackTitle}}.flac",
              "foundTrack": {
                "id": "{{trackId}}",
                "title": "{{trackTitle}}",
                "position": 1
              },
              "mediaFile": {
                "name": "01 {{trackTitle}}.flac",
                "path": "Download/complete/Album/01 {{trackTitle}}.flac"
              },
              "folderMetaData": {
                "basePath": "Music/Some Artist/Some Album",
                "artistName": "Some Artist",
                "releaseName": "Some Album",
                "year": 1972,
                "musicBrainzRelease": {
                  "id": "{{ReleaseId}}",
                  "title": "Some Album",
                  "media": [{ "tracks": [{ "id": "{{trackId}}", "title": "{{trackTitle}}" }] }]
                }
              }
            }
            """;
    }

    private async Task<int> SeedLegacyRowAsync(Guid trackId, string trackTitle)
    {
        await using QueueContext context = await _factory.CreateDbContextAsync();

        QueueJob job = new()
        {
            Queue = "encoder",
            Priority = 5,
            Payload = LegacyPayload(trackId, trackTitle),
            AvailableAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        context.QueueJobs.Add(job);
        await context.SaveChangesAsync();

        return job.Id;
    }

    private QueuePayloadCompaction BuildCompaction()
    {
        return new(_factory, NullLogger<QueuePayloadCompaction>.Instance);
    }

    [Fact]
    public async Task Compaction_LiftsTheReleaseOut_AndLeavesTheRowNamingItsOwnTrack()
    {
        int rowId = await SeedLegacyRowAsync(TrackOneId, "Chug All Night");

        QueuePayloadCompaction compaction = BuildCompaction();
        await compaction.RunAsync();

        await using QueueContext context = await _factory.CreateDbContextAsync();
        QueueJob row = await context.QueueJobs.SingleAsync(job => job.Id == rowId);
        JObject payload = JObject.Parse(row.Payload);

        payload["folderMetaData"]
            .Should()
            .BeNull("the release is what made these payloads a megabyte each");

        GuidOf(payload, "releaseId").Should().Be(ReleaseId);
        GuidOf(payload, "trackId")
            .Should()
            .Be(TrackOneId, "a row that forgets its track is a dropped encode");
        payload
            .Value<string>("basePath")
            .Should()
            .Be(
                "Music/Some Artist/Some Album",
                "the destination was computed at dispatch and must not be re-derived"
            );

        payload
            .Value<string>("$type")
            .Should()
            .Contain(
                "MusicEncodeJob",
                "the row still names the job it is — only the release copy is gone"
            );
    }

    /// <summary>Ids are JSON strings; JToken will not cast one to a Guid.</summary>
    private static Guid GuidOf(JObject payload, string property)
    {
        return Guid.TryParse(payload.Value<string>(property), out Guid parsed)
            ? parsed
            : Guid.Empty;
    }

    [Fact]
    public async Task Compaction_KeepsEveryTrackOfAReleaseWithoutCopyingTheRelease()
    {
        await SeedLegacyRowAsync(TrackOneId, "Chug All Night");
        await SeedLegacyRowAsync(TrackTwoId, "Take It Easy");

        QueuePayloadCompaction compaction = BuildCompaction();
        await compaction.RunAsync();

        await using QueueContext context = await _factory.CreateDbContextAsync();

        context
            .QueueJobs.Select(job => job.Payload)
            .Should()
            .OnlyContain(
                payload => !payload.Contains("folderMetaData"),
                "the release the payload used to carry is not stored anywhere now — the "
                    + "job rebuilds it from its id, out of the provider cache"
            );

        context
            .QueueJobs.Select(job => job.Payload)
            .Select(payload => GuidOf(JObject.Parse(payload), "trackId"))
            .Should()
            .BeEquivalentTo([TrackOneId, TrackTwoId], "each row keeps its own track");
    }

    [Fact]
    public async Task Compaction_IsIdempotent_AndDoesNotRewriteRowsTwice()
    {
        int rowId = await SeedLegacyRowAsync(TrackOneId, "Chug All Night");

        QueuePayloadCompaction compaction = BuildCompaction();
        await compaction.RunAsync();

        await using QueueContext first = await _factory.CreateDbContextAsync();
        string afterFirst = (await first.QueueJobs.SingleAsync(job => job.Id == rowId)).Payload;

        int secondRun = await compaction.RunAsync();

        await using QueueContext second = await _factory.CreateDbContextAsync();
        string afterSecond = (await second.QueueJobs.SingleAsync(job => job.Id == rowId)).Payload;

        secondRun.Should().Be(0, "a compacted row has a hash, which is what marks it done");
        afterSecond.Should().Be(afterFirst);
    }

    private class TestContextFactory(SqliteConnection connection) : IDbContextFactory<QueueContext>
    {
        public QueueContext CreateDbContext()
        {
            DbContextOptions<QueueContext> options = new DbContextOptionsBuilder<QueueContext>()
                .UseSqlite(connection)
                .Options;

            return new(options);
        }
    }
}
