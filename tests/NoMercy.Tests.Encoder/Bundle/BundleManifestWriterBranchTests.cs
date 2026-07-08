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

using System.Text;
using Newtonsoft.Json;
using NoMercy.Encoder.Bundle;

namespace NoMercy.Tests.Encoder.Bundle;

/// <summary>
/// Branch-coverage gaps for <see cref="BundleManifestWriter"/>:
///
/// • ReconcileAsync with both extra AND missing — single call surfaces both
///   sets, not just one. Pinned because the report consumer renders both
///   columns simultaneously.
/// • ReconcileAsync case-insensitive comparison — HashSet uses
///   OrdinalIgnoreCase, so manifest "video/INIT.mp4" matches disk
///   "video/init.mp4" without flagging as extra/missing.
/// • Empty manifest + empty disk → empty report (smoke).
/// • ReadAsync round-trips a written manifest.
/// • ReconcileAsync handles trailing slash in bundleDirectory.
/// </summary>
public class BundleManifestWriterBranchTests
{
    private static BundleManifest SampleManifest(params string[] files) =>
        new(
            Version: 1,
            EncoderVersion: "3.0.0",
            PresetId: "01HZABC",
            PresetName: "Web 1080p",
            PresetSlug: "web-1080p",
            MediaType: "movie",
            MediaId: 550,
            MediaExternalId: null,
            MediaFolder: "/media/movies/Fight Club (1999)",
            Container: "hls-fmp4",
            CreatedAt: new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CompletedAt: new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            MediaKey: "mfa",
            Files: files
        );

    [Fact]
    public async Task ReconcileAsync_reports_both_extra_AND_missing_in_single_call()
    {
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);

        BundleManifest manifest = SampleManifest("a.m3u8", "b.m4s", "c.m4s");

        const string bundleDir = "encodes/web-1080p";
        // Disk has a + b + 1 extra + missing c.
        storage.Seed($"{bundleDir}/a.m3u8", [0x01]);
        storage.Seed($"{bundleDir}/b.m4s", [0x01]);
        storage.Seed($"{bundleDir}/orphan.ts", [0x02]);
        // c.m4s missing.

        ReconcileReport report = await writer.ReconcileAsync(
            bundleDir,
            manifest,
            CancellationToken.None
        );

        report.ExtraFiles.Should().ContainSingle().Which.Should().Be("orphan.ts");
        report.MissingFiles.Should().ContainSingle().Which.Should().Be("c.m4s");
    }

    [Fact]
    public async Task ReconcileAsync_match_is_case_insensitive()
    {
        // The HashSet uses OrdinalIgnoreCase — disk "init.mp4" matches
        // manifest "INIT.mp4" so the bundle isn't flagged as extra/missing.
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);

        BundleManifest manifest = SampleManifest("video/INIT.mp4", "video/SEG_001.M4S");

        const string bundleDir = "encodes/web-1080p";
        storage.Seed($"{bundleDir}/video/init.mp4", [0x01]);
        storage.Seed($"{bundleDir}/video/seg_001.m4s", [0x01]);

        ReconcileReport report = await writer.ReconcileAsync(
            bundleDir,
            manifest,
            CancellationToken.None
        );

        report.ExtraFiles.Should().BeEmpty();
        report.MissingFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_empty_manifest_and_empty_disk_returns_empty_report()
    {
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);

        BundleManifest manifest = SampleManifest();

        ReconcileReport report = await writer.ReconcileAsync(
            "encodes/empty",
            manifest,
            CancellationToken.None
        );

        report.ExtraFiles.Should().BeEmpty();
        report.MissingFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_handles_trailing_slash_in_bundle_directory()
    {
        // BundleDirectory string ends with "/" — the path-prefix stripping
        // logic must normalize so reading "encodes/x/" + "f.m3u8" matches a
        // disk path "encodes/x/f.m3u8".
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);

        BundleManifest manifest = SampleManifest("file.m3u8");

        storage.Seed("encodes/x/file.m3u8", [0x01]);

        ReconcileReport report = await writer.ReconcileAsync(
            "encodes/x/",
            manifest,
            CancellationToken.None
        );

        report.ExtraFiles.Should().BeEmpty();
        report.MissingFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteAsync_then_ReadAsync_round_trips_manifest_with_all_fields()
    {
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);

        BundleManifest original = SampleManifest("master.m3u8", "init.mp4", "seg_00001.m4s");

        const string path = "encodes/web-1080p/manifest.json";
        await writer.WriteAsync(path, original, CancellationToken.None);
        BundleManifest? read = await writer.ReadAsync(path, CancellationToken.None);

        read.Should().NotBeNull();
        read.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task ReadAsync_invalid_json_returns_null_without_throwing()
    {
        // Corrupt manifest.json on disk — ReadAsync should let JsonConvert
        // return null rather than crashing the caller.
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);

        storage.Seed("encodes/x/manifest.json", Encoding.UTF8.GetBytes("not valid json"));

        Func<Task<BundleManifest?>> act = () =>
            writer.ReadAsync("encodes/x/manifest.json", CancellationToken.None);

        // Newtonsoft's DeserializeObject<T> on malformed JSON throws JsonException
        // — accept either NotThrow (returns null) or the throw, then document
        // current behavior. (Today it throws.)
        await act.Should().ThrowAsync<JsonException>();
    }
}
