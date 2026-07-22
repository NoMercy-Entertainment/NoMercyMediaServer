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

using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.MediaProcessing.EventHandlers;

namespace NoMercy.Tests.MediaProcessing.EventHandlers;

/// <summary>
/// Pins the "encode once per media regardless of how many drivers host it"
/// contract that <see cref="AutoEncodeSubscriber"/> delegates to
/// <c>SelectSourcesToEncode</c>. Before this dedup logic shipped, an episode
/// stored on both NFS and S3 produced TWO VideoEncodeJob dispatches — same
/// content encoded twice, once per source. The encoder publishes its output
/// to every configured destination from a single run, so duplicate
/// dispatches were pure waste.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class AutoEncodeDedupTests
{
    private static readonly Ulid LocalDriverId = Ulid.NewUlid();
    private static readonly Ulid NfsDriverId = Ulid.NewUlid();
    private static readonly Ulid SmbDriverId = Ulid.NewUlid();
    private static readonly Ulid WebdavDriverId = Ulid.NewUlid();
    private static readonly Ulid S3DriverId = Ulid.NewUlid();
    private static readonly Ulid R2DriverId = Ulid.NewUlid();

    private static Dictionary<Ulid, string> StandardDriverMap() =>
        new()
        {
            { LocalDriverId, "local" },
            { NfsDriverId, "nfs" },
            { SmbDriverId, "smb" },
            { WebdavDriverId, "webdav" },
            { S3DriverId, "s3" },
            { R2DriverId, "r2" },
        };

    private static Folder Folder(Ulid id, string path, Ulid driverId) =>
        new()
        {
            Id = id,
            Path = path,
            DriverId = driverId,
        };

    private static VideoFile Video(string hostFolder, string filename, int episodeId = 100) =>
        new()
        {
            Id = Ulid.NewUlid(),
            HostFolder = hostFolder,
            Filename = filename,
            EpisodeId = episodeId,
            Quality = "1080p",
            Share = "remote",
            Languages = "eng",
        };

    [Fact]
    public void Same_filename_on_NFS_and_S3_dispatches_one_encode_against_NFS()
    {
        // The actual bug we shipped: an episode existed on both Anime/Anime
        // (NFS) and Anime-S3 (S3). Pre-fix the loop dispatched twice.
        Folder nfsFolder = Folder(id: Ulid.NewUlid(), path: "/mnt/vault/Media/Anime/Anime", driverId: NfsDriverId);
        Folder s3Folder = Folder(id: Ulid.NewUlid(), path: "Anime-S3", driverId: S3DriverId);

        VideoFile nfsVideo = Video(
            hostFolder: "/mnt/vault/Media/Anime/Anime/Black.Butler.(2008)/Black.Butler.S05E02",
            filename: "/Black.Butler.S05E02.NoMercy.m3u8"
        );
        VideoFile s3Video = Video(
            hostFolder: "Anime-S3/Black.Butler.(2008)/Black.Butler.S05E02",
            filename: "/Black.Butler.S05E02.NoMercy.m3u8"
        );

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(videoFiles: [nfsVideo, s3Video], folders: [nfsFolder, s3Folder], driverTypeById: StandardDriverMap())
            .ToList();

        picked.Should().HaveCount(expected: 1, because: "one filename, one encode — encoder publishes to all dests");
        picked[index: 0].File.HostFolder.Should().StartWith(expected: "/mnt/vault", because: "NFS preferred over S3");
        picked[index: 0].Folder.Id.Should().Be(expected: nfsFolder.Id);
    }

    [Fact]
    public void Local_beats_every_other_driver_type()
    {
        Folder localFolder = Folder(id: Ulid.NewUlid(), path: "/local/media", driverId: LocalDriverId);
        Folder nfsFolder = Folder(id: Ulid.NewUlid(), path: "/mnt/nfs", driverId: NfsDriverId);

        VideoFile localFile = Video(hostFolder: "/local/media/show", filename: "/ep.m3u8");
        VideoFile nfsFile = Video(hostFolder: "/mnt/nfs/show", filename: "/ep.m3u8");

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(
                videoFiles: [nfsFile, localFile],
                folders: [localFolder, nfsFolder],
                driverTypeById: StandardDriverMap()
            )
            .ToList();

        picked.Should().HaveCount(expected: 1);
        picked[index: 0].File.HostFolder.Should().Be(expected: "/local/media/show");
    }

    [Fact]
    public void NFS_only_episode_dispatches_against_NFS()
    {
        // S05E07 / S05E08 / S00E03 in Stoney's library: only on NFS, not S3.
        // Single source → single dispatch, no special case.
        Folder nfsFolder = Folder(id: Ulid.NewUlid(), path: "/mnt/vault/Media/Anime/Anime", driverId: NfsDriverId);
        Folder s3Folder = Folder(id: Ulid.NewUlid(), path: "Anime-S3", driverId: S3DriverId);

        VideoFile only = Video(
            hostFolder: "/mnt/vault/Media/Anime/Anime/Black.Butler.(2008)/Black.Butler.S05E07",
            filename: "/Black.Butler.S05E07.NoMercy.m3u8"
        );

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(videoFiles: [only], folders: [nfsFolder, s3Folder], driverTypeById: StandardDriverMap())
            .ToList();

        picked.Should().HaveCount(expected: 1);
        picked[index: 0].File.HostFolder.Should().StartWith(expected: "/mnt/vault");
    }

    [Fact]
    public void Multiple_episodes_each_get_one_dispatch()
    {
        // 13 distinct episode files across NFS+S3 → 13 dispatches, not 26.
        Folder nfsFolder = Folder(id: Ulid.NewUlid(), path: "/mnt/vault/Media/Anime/Anime", driverId: NfsDriverId);
        Folder s3Folder = Folder(id: Ulid.NewUlid(), path: "Anime-S3", driverId: S3DriverId);

        List<VideoFile> videos = [];
        for (int ep = 1; ep <= 13; ep++)
        {
            string filename = $"/Black.Butler.S05E{ep:D2}.NoMercy.m3u8";
            videos.Add(
                item: Video(
                    hostFolder: $"/mnt/vault/Media/Anime/Anime/Black.Butler.(2008)/Black.Butler.S05E{ep:D2}",
                    filename: filename
                )
            );
            videos.Add(item: Video(hostFolder: $"Anime-S3/Black.Butler.(2008)/Black.Butler.S05E{ep:D2}", filename: filename));
        }

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(videoFiles: videos, folders: [nfsFolder, s3Folder], driverTypeById: StandardDriverMap())
            .ToList();

        picked.Should().HaveCount(expected: 13, because: "one dispatch per filename");
        picked.Should().AllSatisfy(expected: p => p.File.HostFolder.Should().StartWith(expected: "/mnt/vault"));
    }

    [Fact]
    public void Source_with_no_matching_folder_is_skipped()
    {
        // VideoFile pointing at a path that no Folder claims — orphan record,
        // should not produce a dispatch.
        Folder nfsFolder = Folder(id: Ulid.NewUlid(), path: "/mnt/vault/Media/Anime", driverId: NfsDriverId);
        VideoFile orphan = Video(hostFolder: "/somewhere/else", filename: "/file.m3u8");

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(videoFiles: [orphan], folders: [nfsFolder], driverTypeById: StandardDriverMap())
            .ToList();

        picked.Should().BeEmpty(because: "orphan VideoFile rows shouldn't trigger an encode");
    }

    [Fact]
    public void Driver_preference_order_is_local_nfs_smb_webdav_s3_r2()
    {
        // Sanity check: the ranking enum.
        AutoEncodeSubscriber
            .DriverPreference(typeById: StandardDriverMap(), driverId: LocalDriverId)
            .Should()
            .BeLessThan(expected: AutoEncodeSubscriber.DriverPreference(typeById: StandardDriverMap(), driverId: NfsDriverId));
        AutoEncodeSubscriber
            .DriverPreference(typeById: StandardDriverMap(), driverId: NfsDriverId)
            .Should()
            .Be(expected: AutoEncodeSubscriber.DriverPreference(typeById: StandardDriverMap(), driverId: SmbDriverId));
        AutoEncodeSubscriber
            .DriverPreference(typeById: StandardDriverMap(), driverId: NfsDriverId)
            .Should()
            .BeLessThan(expected: AutoEncodeSubscriber.DriverPreference(typeById: StandardDriverMap(), driverId: WebdavDriverId));
        AutoEncodeSubscriber
            .DriverPreference(typeById: StandardDriverMap(), driverId: WebdavDriverId)
            .Should()
            .BeLessThan(expected: AutoEncodeSubscriber.DriverPreference(typeById: StandardDriverMap(), driverId: S3DriverId));
        AutoEncodeSubscriber
            .DriverPreference(typeById: StandardDriverMap(), driverId: S3DriverId)
            .Should()
            .Be(expected: AutoEncodeSubscriber.DriverPreference(typeById: StandardDriverMap(), driverId: R2DriverId));
    }

    [Fact]
    public void Unknown_driver_id_falls_to_back_of_preference_queue()
    {
        Ulid mysteryDriver = Ulid.NewUlid();
        AutoEncodeSubscriber
            .DriverPreference(typeById: StandardDriverMap(), driverId: mysteryDriver)
            .Should()
            .BeGreaterThan(
                expected: AutoEncodeSubscriber.DriverPreference(typeById: StandardDriverMap(), driverId: R2DriverId),
                because: "unknown drivers shouldn't beat known ones"
            );
    }

    [Fact]
    public void Empty_video_files_returns_empty()
    {
        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(videoFiles: [], folders: [], driverTypeById: StandardDriverMap())
            .ToList();

        picked.Should().BeEmpty();
    }

    [Fact]
    public void Three_source_drivers_picks_the_one_with_lowest_preference()
    {
        // Same content on local + nfs + s3 → local wins.
        Folder localFolder = Folder(id: Ulid.NewUlid(), path: "/local", driverId: LocalDriverId);
        Folder nfsFolder = Folder(id: Ulid.NewUlid(), path: "/nfs", driverId: NfsDriverId);
        Folder s3Folder = Folder(id: Ulid.NewUlid(), path: "/s3", driverId: S3DriverId);

        VideoFile l = Video(hostFolder: "/local/show", filename: "/ep.m3u8");
        VideoFile n = Video(hostFolder: "/nfs/show", filename: "/ep.m3u8");
        VideoFile s = Video(hostFolder: "/s3/show", filename: "/ep.m3u8");

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(
                videoFiles: [s, n, l],
                folders: [localFolder, nfsFolder, s3Folder],
                driverTypeById: StandardDriverMap()
            )
            .ToList();

        picked.Should().HaveCount(expected: 1);
        picked[index: 0].File.HostFolder.Should().Be(expected: "/local/show");
    }
}
