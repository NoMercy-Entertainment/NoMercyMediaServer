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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Storage;
using NoMercy.Tests.Repositories.Infrastructure;

namespace NoMercy.Tests.Repositories;

[Trait("Category", "Unit")]
public class TrackMetadataCascadeTests : IDisposable
{
    private readonly IDbContextFactory<MediaContext> _factory;
    private readonly SqliteConnection _connection;

    private static readonly Ulid FolderId = Ulid.NewUlid();
    private static readonly Ulid MetadataId = Ulid.NewUlid();
    private static readonly Guid AudioTrackId = Guid.NewGuid();
    private static readonly Guid SiblingTrack1Id = Guid.NewGuid();
    private static readonly Guid SiblingTrack2Id = Guid.NewGuid();

    public TrackMetadataCascadeTests()
    {
        (_factory, _connection) = TestMediaContextFactory.CreateFactory();
    }

    // Regression test for the Metadata<->Track mutual-cascade bug: Metadata.AudioTrackId
    // and Track.MetadataId both cascaded, so deleting the one Track that a Metadata row
    // pointed to as its AudioTrack deleted that Metadata, which in turn deleted every
    // other Track sharing the same MetadataId. Deleting one track must never touch its
    // siblings or the shared Metadata row.
    [Fact]
    public void DeletingTrack_PreservesSharedMetadataAndSiblingTracks()
    {
        using (MediaContext seedContext = _factory.CreateDbContext())
        {
            SeedSharedMetadataWithTwoSiblingTracks(seedContext);

            Track trackToDelete = seedContext.Tracks.Single(t => t.Id == AudioTrackId);
            seedContext.Tracks.Remove(trackToDelete);
            seedContext.SaveChanges();
        }

        // Re-query from a fresh context against the same underlying database so the
        // assertion proves the persisted schema behavior, not the first context's
        // in-memory change tracker.
        using MediaContext assertContext = _factory.CreateDbContext();

        Assert.NotNull(assertContext.Metadata.Find(MetadataId));
        Assert.NotNull(assertContext.Tracks.Find(SiblingTrack1Id));
        Assert.NotNull(assertContext.Tracks.Find(SiblingTrack2Id));
    }

    private static void SeedSharedMetadataWithTwoSiblingTracks(MediaContext context)
    {
        Driver driver = new()
        {
            Id = Driver.SystemLocalDriverId,
            Name = "Local Filesystem",
            Type = "local",
            Config = """{"rootPath":"/"}""",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Drivers.Add(driver);

        Folder folder = new()
        {
            Id = FolderId,
            Path = "/media/music/Artist/Album",
            DriverId = Driver.SystemLocalDriverId,
        };
        context.Folders.Add(folder);

        Track audioTrack = new()
        {
            Id = AudioTrackId,
            Name = "Track One",
            FolderId = FolderId,
            LibraryFolder = folder,
        };
        Track siblingTrack1 = new()
        {
            Id = SiblingTrack1Id,
            Name = "Track Two",
            FolderId = FolderId,
            LibraryFolder = folder,
        };
        Track siblingTrack2 = new()
        {
            Id = SiblingTrack2Id,
            Name = "Track Three",
            FolderId = FolderId,
            LibraryFolder = folder,
        };
        context.Tracks.AddRange([audioTrack, siblingTrack1, siblingTrack2]);
        context.SaveChanges();

        Metadata metadata = new()
        {
            Id = MetadataId,
            Type = MediaType.Music,
            Filename = "album.flac",
            Folder = "/media/music/Artist/Album",
            HostFolder = "/media/music/Artist/Album",
            AudioTrackId = AudioTrackId,
        };
        context.Metadata.Add(metadata);
        context.SaveChanges();

        // Metadata.AudioTrackId needed the Track rows to exist first, so the sibling
        // link back to Metadata is backfilled here instead of in the same insert batch.
        siblingTrack1.MetadataId = MetadataId;
        siblingTrack2.MetadataId = MetadataId;
        context.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
