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
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.Storage;
using NoMercy.NmSystem.Domain;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

/// <summary>
/// A queued album is one card that fills up, not a card per track.
/// <para>
/// A twenty-track release drew twenty cards, each sitting at nothing until its
/// own second of encoding, so the panel showed no movement at all while the
/// server worked steadily through an album — and every other kind of work was
/// pushed off the screen. One card counting its tracks says the same thing in
/// one row, and it moves.
/// </para>
/// </summary>
[Trait("Category", "Tasks")]
public class TasksControllerAlbumCardTests : IClassFixture<NoMercyApiFactory>, IAsyncLifetime
{
    private const int EncodedTracks = 3;
    private const int QueuedTracks = 5;
    private const int AlbumTracks = EncodedTracks + QueuedTracks;
    private static readonly Guid ReleaseId = new("bb2d2f61-4d43-4f2a-9d51-19f3d3e2c0aa");

    private readonly HttpClient _authed;
    private readonly List<int> _rowIds = [];
    private readonly Ulid _libraryId = Ulid.NewUlid();
    private readonly Ulid _folderId = Ulid.NewUlid();

    public TasksControllerAlbumCardTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
    }

    private static Guid EncodedTrackId(int track) =>
        new($"0000000{track}-3333-3333-3333-333333333333");

    private static string MusicPayload(int track)
    {
        return $$"""
            {
              "$type": "NoMercy.MediaProcessing.Jobs.MediaJobs.MusicEncodeJob, NoMercy.MediaProcessing",
              "queueName": "encoder",
              "priority": 5,
              "libraryId": "01HQ5W4JJ9ZAX7721AQJZFQ7E1",
              "folderId": "01KXXNPYFBZF9ARJ8ZE2X6XCAP",
              "releaseId": "{{ReleaseId}}",
              "trackId": "0000000{{track}}-2222-2222-2222-222222222222",
              "artistName": "Eagles",
              "releaseName": "Hotel California",
              "inputFile": "Download/complete/Eagles/0{{track}}.flac"
            }
            """;
    }

    public async Task InitializeAsync()
    {
        await using MediaContext media = new();

        Library library = new()
        {
            Id = _libraryId,
            Title = "Album card music",
            Type = "music",
        };
        Folder folder = new()
        {
            Id = _folderId,
            Path = "Music/Eagles",
            DriverId = Driver.SystemLocalDriverId,
        };

        media.Libraries.Add(library);
        media.Folders.Add(folder);
        media.FolderLibrary.Add(new(_folderId, _libraryId));
        await media.SaveChangesAsync();

        // Both navigations are default-initialized on the model, so an album that
        // only sets the ids drags two empty rows into the insert and the foreign
        // keys fail on them rather than on anything this test is about.
        //
        // Tracks is deliberately a number that matches nothing: it is what the
        // metadata provider says the release holds, and a card that counts
        // towards it reports work as done that was never encoded.
        media.Albums.Add(
            new()
            {
                Id = ReleaseId,
                Name = "Hotel California",
                LibraryId = _libraryId,
                Library = library,
                FolderId = _folderId,
                LibraryFolder = folder,
                Tracks = 42,
            }
        );
        await media.SaveChangesAsync();

        // Tracks already encoded. The link to the release is written when the
        // encode succeeds and the recording is stored, so these rows are the
        // finished work — and the only honest numerator the card has.
        for (int track = 1; track <= EncodedTracks; track++)
        {
            Guid trackId = EncodedTrackId(track);
            media.Tracks.Add(
                new()
                {
                    Id = trackId,
                    Name = $"Encoded {track}",
                    FolderId = _folderId,
                    Folder = "/Music/Eagles",
                    Filename = $"/e{track}.flac",
                }
            );
            media.AlbumTrack.Add(new(ReleaseId, trackId));
        }

        await media.SaveChangesAsync();

        await using QueueContext queue = new();
        List<QueueJob> rows = [];
        for (int track = 1; track <= QueuedTracks; track++)
            rows.Add(
                new()
                {
                    Queue = "encoder",
                    Priority = 5,
                    Payload = MusicPayload(track),
                    AvailableAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                }
            );

        queue.QueueJobs.AddRange(rows);
        await queue.SaveChangesAsync();
        _rowIds.AddRange(rows.Select(row => row.Id));
    }

    public async Task DisposeAsync()
    {
        await using QueueContext queue = new();
        await queue.QueueJobs.Where(job => _rowIds.Contains(job.Id)).ExecuteDeleteAsync();

        await using MediaContext media = new();
        List<Guid> trackIds = Enumerable.Range(1, EncodedTracks).Select(EncodedTrackId).ToList();
        await media.AlbumTrack.Where(link => link.AlbumId == ReleaseId).ExecuteDeleteAsync();
        await media.Tracks.Where(track => trackIds.Contains(track.Id)).ExecuteDeleteAsync();
        await media.Albums.Where(album => album.Id == ReleaseId).ExecuteDeleteAsync();
        await media.FolderLibrary.Where(link => link.FolderId == _folderId).ExecuteDeleteAsync();
        await media.Folders.Where(folder => folder.Id == _folderId).ExecuteDeleteAsync();
        await media.Libraries.Where(library => library.Id == _libraryId).ExecuteDeleteAsync();
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

    private async Task<JsonElement> AlbumCardAsync()
    {
        JsonElement[] rows = await GetQueueAsync();
        return rows.Single(row =>
            row.GetProperty("payload_id").GetString() == ReleaseId.ToString()
        );
    }

    [Fact]
    public async Task AnAlbum_IsOneCard_NotOnePerTrack()
    {
        JsonElement[] rows = await GetQueueAsync();

        rows.Count(row => row.GetProperty("payload_id").GetString() == ReleaseId.ToString())
            .Should()
            .Be(
                1,
                "{0} queued tracks of one release are one piece of work — a card each "
                    + "pushes every other job off the panel and none of them ever moves",
                QueuedTracks
            );
    }

    [Fact]
    public async Task TheAlbumCard_CountsItsTracksTowardsTheAlbumsTotal()
    {
        JsonElement card = await AlbumCardAsync();

        card.GetProperty("total_items")
            .GetInt32()
            .Should()
            .Be(
                AlbumTracks,
                "the album is what is stored plus what is still queued, so the card "
                    + "reaches 100% exactly when the queue for it runs dry"
            );
        card.GetProperty("completed_items")
            .GetInt32()
            .Should()
            .Be(EncodedTracks, "a stored track is an encoded track — nothing else is done");
        card.GetProperty("progress")
            .GetDouble()
            .Should()
            .BeApproximately(EncodedTracks * 100d / AlbumTracks, 0.05);
    }

    [Fact]
    public async Task TheAlbumCard_NamesTheReleaseAndReadsAsAnEncode()
    {
        JsonElement card = await AlbumCardAsync();

        card.GetProperty("title").GetString().Should().Contain("Hotel California");
        card.GetProperty("status").GetString().Should().Be("pending");
        card.GetProperty("kind")
            .ValueKind.Should()
            .Be(
                JsonValueKind.Null,
                "a kind marks a row as maintenance, and the dashboard draws anything it "
                    + "does not recognise as generic maintenance — no artwork, no progress"
            );
    }
}
