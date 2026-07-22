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

using System.Collections.Concurrent;
using NoMercy.NmSystem.Dto;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Reproduces the production scan regression where a freshly-encoded HLS show
/// (episode folders each holding a <c>*.NoMercy.m3u8</c> master) is enumerated as
/// zero files. Drives the real <see cref="MediaScan"/> exactly as
/// <c>FileManager.GetFiles</c> does for an anime library (file listing on, regex
/// filter off, video extension filter, depth 2), over a temp tree.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class MediaScanHlsDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        path1: Path.GetTempPath(),
        path2: "nm_scan_" + Guid.NewGuid().ToString(format: "N")
    );

    // The exact production folder name for Hensuki (id 88061): embeds a comma and
    // an apostrophe. The control is a clean name (One Piece style) that is known
    // to scan correctly in production.
    private const string HensukiName =
        "Hensuki.Are.You.Willing.to.Fall.in.Love.With.a.Pervert,.As.Long.As.She's.a.Cutie.(2019)";
    private const string CleanName = "One.Piece.(1999)";

    [Theory]
    [InlineData(data: [HensukiName, "Hensuki.Are.You.Willing.to.Fall.in.Love.With.a.Pervert,.As.Long.As.She's.a.Cutie"])]
    [InlineData(data: [CleanName, "One.Piece"])]
    public async Task Process_FindsHlsMasters_ForEachEpisodeFolder(
        string showFolderName,
        string episodeBase
    )
    {
        string showFolder = Path.Combine(path1: _root, path2: showFolderName);
        Directory.CreateDirectory(path: showFolder);

        for (int ep = 1; ep <= 3; ep++)
        {
            string episodeFolder = Path.Combine(path1: showFolder, path2: $"{episodeBase}.S01E{ep:D2}");
            Directory.CreateDirectory(path: episodeFolder);

            File.WriteAllText(
                path: Path.Combine(
                    path1: episodeFolder,
                    path2: $"{episodeBase}.S01E{ep:D2}.Episode.Title.NoMercy.m3u8"
                ),
                contents: "#EXTM3U\n#EXT-X-VERSION:6\n"
            );
            File.WriteAllText(path: Path.Combine(path1: episodeFolder, path2: "chapters.vtt"), contents: "WEBVTT\n");
            File.WriteAllText(path: Path.Combine(path1: episodeFolder, path2: "fonts.json"), contents: "[]");
            File.WriteAllText(path: Path.Combine(path1: episodeFolder, path2: "thumbs_160x90.vtt"), contents: "WEBVTT\n");
            foreach (
                string sub in new[]
                {
                    "video_1920x1080_SDR",
                    "audio_eng_aac",
                    "audio_jpn_aac",
                    "subtitles",
                    "fonts",
                }
            )
                Directory.CreateDirectory(path: Path.Combine(path1: episodeFolder, path2: sub));
        }

        MediaScan mediaScan = new(driver: new LocalStorageDriver());
        ConcurrentBag<MediaFolderExtend> folders = await mediaScan
            .EnableFileListing()
            .DisableRegexFilter()
            .FilterByMediaType(mediaType: "anime")
            .FilterByFileName(filter: null)
            .Process(rootFolder: showFolder, depth: 2);
        await mediaScan.DisposeAsync();

        int rawFileCount = folders.Sum(selector: folder => folder.Files?.Count ?? 0);
        bool hasCandidates = folders
            .SelectMany(selector: folder => folder.Files ?? [])
            .Any(predicate: file => file.Parsed is not null);

        rawFileCount.Should().Be(expected: 3, because: "each of the 3 episode folders holds one .NoMercy.m3u8 master");
        hasCandidates.Should().BeTrue(because: "the discovered m3u8 masters must be parseable candidates");
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _root))
            Directory.Delete(path: _root, recursive: true);
        GC.SuppressFinalize(obj: this);
    }
}
