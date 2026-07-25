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
[Trait("Category", "Unit")]
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
        Folder nfsFolder = Folder(Ulid.NewUlid(), "/mnt/vault/Media/Anime/Anime", NfsDriverId);
        Folder s3Folder = Folder(Ulid.NewUlid(), "Anime-S3", S3DriverId);

        VideoFile nfsVideo = Video(
            "/mnt/vault/Media/Anime/Anime/Black.Butler.(2008)/Black.Butler.S05E02",
            "/Black.Butler.S05E02.NoMercy.m3u8"
        );
        VideoFile s3Video = Video(
            "Anime-S3/Black.Butler.(2008)/Black.Butler.S05E02",
            "/Black.Butler.S05E02.NoMercy.m3u8"
        );

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode([nfsVideo, s3Video], [nfsFolder, s3Folder], StandardDriverMap())
            .ToList();

        picked.Should().HaveCount(1, "one filename, one encode — encoder publishes to all dests");
        picked[0].File.HostFolder.Should().StartWith("/mnt/vault", "NFS preferred over S3");
        picked[0].Folder.Id.Should().Be(nfsFolder.Id);
    }

    [Fact]
    public void Local_beats_every_other_driver_type()
    {
        Folder localFolder = Folder(Ulid.NewUlid(), "/local/media", LocalDriverId);
        Folder nfsFolder = Folder(Ulid.NewUlid(), "/mnt/nfs", NfsDriverId);

        VideoFile localFile = Video("/local/media/show", "/ep.m3u8");
        VideoFile nfsFile = Video("/mnt/nfs/show", "/ep.m3u8");

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(
                [nfsFile, localFile],
                [localFolder, nfsFolder],
                StandardDriverMap()
            )
            .ToList();

        picked.Should().HaveCount(1);
        picked[0].File.HostFolder.Should().Be("/local/media/show");
    }

    [Fact]
    public void NFS_only_episode_dispatches_against_NFS()
    {
        // S05E07 / S05E08 / S00E03 in Stoney's library: only on NFS, not S3.
        // Single source → single dispatch, no special case.
        Folder nfsFolder = Folder(Ulid.NewUlid(), "/mnt/vault/Media/Anime/Anime", NfsDriverId);
        Folder s3Folder = Folder(Ulid.NewUlid(), "Anime-S3", S3DriverId);

        VideoFile only = Video(
            "/mnt/vault/Media/Anime/Anime/Black.Butler.(2008)/Black.Butler.S05E07",
            "/Black.Butler.S05E07.NoMercy.m3u8"
        );

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode([only], [nfsFolder, s3Folder], StandardDriverMap())
            .ToList();

        picked.Should().HaveCount(1);
        picked[0].File.HostFolder.Should().StartWith("/mnt/vault");
    }

    [Fact]
    public void Multiple_episodes_each_get_one_dispatch()
    {
        // 13 distinct episode files across NFS+S3 → 13 dispatches, not 26.
        Folder nfsFolder = Folder(Ulid.NewUlid(), "/mnt/vault/Media/Anime/Anime", NfsDriverId);
        Folder s3Folder = Folder(Ulid.NewUlid(), "Anime-S3", S3DriverId);

        List<VideoFile> videos = [];
        for (int ep = 1; ep <= 13; ep++)
        {
            string filename = $"/Black.Butler.S05E{ep:D2}.NoMercy.m3u8";
            videos.Add(
                Video(
                    $"/mnt/vault/Media/Anime/Anime/Black.Butler.(2008)/Black.Butler.S05E{ep:D2}",
                    filename
                )
            );
            videos.Add(Video($"Anime-S3/Black.Butler.(2008)/Black.Butler.S05E{ep:D2}", filename));
        }

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(videos, [nfsFolder, s3Folder], StandardDriverMap())
            .ToList();

        picked.Should().HaveCount(13, "one dispatch per filename");
        picked.Should().AllSatisfy(p => p.File.HostFolder.Should().StartWith("/mnt/vault"));
    }

    [Fact]
    public void Source_with_no_matching_folder_is_skipped()
    {
        // VideoFile pointing at a path that no Folder claims — orphan record,
        // should not produce a dispatch.
        Folder nfsFolder = Folder(Ulid.NewUlid(), "/mnt/vault/Media/Anime", NfsDriverId);
        VideoFile orphan = Video("/somewhere/else", "/file.m3u8");

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode([orphan], [nfsFolder], StandardDriverMap())
            .ToList();

        picked.Should().BeEmpty("orphan VideoFile rows shouldn't trigger an encode");
    }

    [Fact]
    public void Driver_preference_order_is_local_nfs_smb_webdav_s3_r2()
    {
        // Sanity check: the ranking enum.
        AutoEncodeSubscriber
            .DriverPreference(StandardDriverMap(), LocalDriverId)
            .Should()
            .BeLessThan(AutoEncodeSubscriber.DriverPreference(StandardDriverMap(), NfsDriverId));
        AutoEncodeSubscriber
            .DriverPreference(StandardDriverMap(), NfsDriverId)
            .Should()
            .Be(AutoEncodeSubscriber.DriverPreference(StandardDriverMap(), SmbDriverId));
        AutoEncodeSubscriber
            .DriverPreference(StandardDriverMap(), NfsDriverId)
            .Should()
            .BeLessThan(AutoEncodeSubscriber.DriverPreference(StandardDriverMap(), WebdavDriverId));
        AutoEncodeSubscriber
            .DriverPreference(StandardDriverMap(), WebdavDriverId)
            .Should()
            .BeLessThan(AutoEncodeSubscriber.DriverPreference(StandardDriverMap(), S3DriverId));
        AutoEncodeSubscriber
            .DriverPreference(StandardDriverMap(), S3DriverId)
            .Should()
            .Be(AutoEncodeSubscriber.DriverPreference(StandardDriverMap(), R2DriverId));
    }

    [Fact]
    public void Unknown_driver_id_falls_to_back_of_preference_queue()
    {
        Ulid mysteryDriver = Ulid.NewUlid();
        AutoEncodeSubscriber
            .DriverPreference(StandardDriverMap(), mysteryDriver)
            .Should()
            .BeGreaterThan(
                AutoEncodeSubscriber.DriverPreference(StandardDriverMap(), R2DriverId),
                "unknown drivers shouldn't beat known ones"
            );
    }

    [Fact]
    public void Empty_video_files_returns_empty()
    {
        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode([], [], StandardDriverMap())
            .ToList();

        picked.Should().BeEmpty();
    }

    [Fact]
    public void Three_source_drivers_picks_the_one_with_lowest_preference()
    {
        // Same content on local + nfs + s3 → local wins.
        Folder localFolder = Folder(Ulid.NewUlid(), "/local", LocalDriverId);
        Folder nfsFolder = Folder(Ulid.NewUlid(), "/nfs", NfsDriverId);
        Folder s3Folder = Folder(Ulid.NewUlid(), "/s3", S3DriverId);

        VideoFile l = Video("/local/show", "/ep.m3u8");
        VideoFile n = Video("/nfs/show", "/ep.m3u8");
        VideoFile s = Video("/s3/show", "/ep.m3u8");

        List<(VideoFile File, Folder Folder)> picked = AutoEncodeSubscriber
            .SelectSourcesToEncode(
                [s, n, l],
                [localFolder, nfsFolder, s3Folder],
                StandardDriverMap()
            )
            .ToList();

        picked.Should().HaveCount(1);
        picked[0].File.HostFolder.Should().Be("/local/show");
    }
}
