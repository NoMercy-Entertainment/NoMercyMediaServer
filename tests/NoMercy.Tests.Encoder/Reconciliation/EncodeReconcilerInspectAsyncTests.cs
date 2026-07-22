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
using NoMercy.Encoder.Reconciliation;
using NoMercy.Tests.Encoder.Bundle;

namespace NoMercy.Tests.Encoder.Reconciliation;

/// <summary>
/// Exercises <see cref="EncodeReconciler.InspectAsync"/> against the
/// in-memory <see cref="TestStorage"/> fake — the real on-disk gathering
/// half of reconciliation, as opposed to <see cref="EncodeReconcilerDecideTests"/>
/// which covers the pure decision.
/// </summary>
public class EncodeReconcilerInspectAsyncTests
{
    private readonly EncodeReconciler _reconciler = new();

    [Fact]
    public async Task InspectAsync_ReturnsAnEmptySnapshot_WhenTheMediaRootHasNothingOnDisk()
    {
        TestStorage storage = new();

        ExistingOutputSnapshot snapshot = await _reconciler.InspectAsync(
            mediaRootPath: "Show/Show S01E01",
            presetId: "preset-id",
            destinationStorage: storage,
            ct: CancellationToken.None
        );

        snapshot.BundleFiles.Should().BeEmpty();
        snapshot.ValidOcrSidecarCount.Should().Be(expected: 0);
        snapshot.ProfileFingerprint.Should().BeNull();
    }

    [Fact]
    public async Task InspectAsync_ListsFilesRelativeToTheMediaRoot_AndCountsOnlyValidOcrSidecars()
    {
        // An OCR sidecar is its bitmap track's sibling — same {lang}.{type} — so
        // it is counted by pairing, not by any marker in the name.
        TestStorage storage = new();
        const string root = "Show/Show S01E01";
        const string stem = "Show.S01E01.NoMercy";
        storage.Seed(path: $"{root}/web-1080p_master.m3u8", bytes: [0x01, 0x02]);
        storage.Seed(path: $"{root}/subtitles/{stem}.eng.full.mks", bytes: [0x01, 0x02]);
        storage.Seed(path: $"{root}/subtitles/{stem}.eng.full.vtt", bytes: [0x01, 0x02, 0x03]);
        storage.Seed(path: $"{root}/subtitles/{stem}.eng.sign.mks", bytes: [0x01, 0x02]);
        storage.Seed(path: $"{root}/subtitles/{stem}.eng.sign.vtt", bytes: []); // truncated — must not count

        ExistingOutputSnapshot snapshot = await _reconciler.InspectAsync(
            mediaRootPath: root,
            presetId: "preset-id",
            destinationStorage: storage,
            ct: CancellationToken.None
        );

        snapshot
            .BundleFiles.Select(selector: f => f.RelativePath)
            .Should()
            .BeEquivalentTo(expectation: ["web-1080p_master.m3u8", $"subtitles/{stem}.eng.full.mks", $"subtitles/{stem}.eng.full.vtt", $"subtitles/{stem}.eng.sign.mks", $"subtitles/{stem}.eng.sign.vtt"]
            );
        snapshot
            .ValidOcrSidecarCount.Should()
            .Be(
                expected: 1,
                because: "the sign track's sidecar is truncated (zero bytes) and must not count as valid"
            );
    }

    [Fact]
    public async Task InspectAsync_ReadsTheStoredProfileFingerprint_WhenTheBlueprintIsPresent()
    {
        TestStorage storage = new();
        const string root = "Show/Show S01E01";
        MediaBlueprint blueprint = MakeBlueprint(
            encode: MakeEncode(presetId: "preset-id", profileFingerprint: "deadbeef")
        );
        storage.Seed(
            path: $"{root}/{MediaBlueprintWriter.FileName}",
            bytes: Encoding.UTF8.GetBytes(s: JsonConvert.SerializeObject(value: blueprint))
        );

        ExistingOutputSnapshot snapshot = await _reconciler.InspectAsync(
            mediaRootPath: root,
            presetId: "preset-id",
            destinationStorage: storage,
            ct: CancellationToken.None
        );

        snapshot.ProfileFingerprint.Should().Be(expected: "deadbeef");
    }

    [Fact]
    public async Task InspectAsync_ReturnsNullFingerprint_WhenTheBlueprintHasNoMatchingPresetEntry()
    {
        TestStorage storage = new();
        const string root = "Show/Show S01E01";
        // Blueprint carries a sibling preset's entry only — the one being
        // reconciled has never been encoded yet.
        MediaBlueprint blueprint = MakeBlueprint(
            encode: MakeEncode(presetId: "other-preset-id", profileFingerprint: "cafebabe")
        );
        storage.Seed(
            path: $"{root}/{MediaBlueprintWriter.FileName}",
            bytes: Encoding.UTF8.GetBytes(s: JsonConvert.SerializeObject(value: blueprint))
        );

        ExistingOutputSnapshot snapshot = await _reconciler.InspectAsync(
            mediaRootPath: root,
            presetId: "preset-id",
            destinationStorage: storage,
            ct: CancellationToken.None
        );

        snapshot.ProfileFingerprint.Should().BeNull();
    }

    [Fact]
    public async Task InspectAsync_ReturnsNullFingerprint_WhenTheBlueprintHasNoneRecorded_LegacyOutput()
    {
        TestStorage storage = new();
        const string root = "Show/Show S01E01";
        MediaBlueprint blueprint = MakeBlueprint(
            encode: MakeEncode(presetId: "preset-id", profileFingerprint: null)
        );
        storage.Seed(
            path: $"{root}/{MediaBlueprintWriter.FileName}",
            bytes: Encoding.UTF8.GetBytes(s: JsonConvert.SerializeObject(value: blueprint))
        );

        ExistingOutputSnapshot snapshot = await _reconciler.InspectAsync(
            mediaRootPath: root,
            presetId: "preset-id",
            destinationStorage: storage,
            ct: CancellationToken.None
        );

        snapshot.ProfileFingerprint.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static MediaBlueprint MakeBlueprint(BlueprintEncode encode) =>
        new(
            Version: 1,
            Identity: new(
                Type: "episode",
                TmdbId: 1,
                Show: new(TmdbId: 2, Title: "Show"),
                Season: 1,
                Episode: 1,
                Title: "Episode",
                Year: 2024
            ),
            Source: new(
                Path: "Download/Show/Show.S01E01.mkv",
                Filename: "Show.S01E01.mkv",
                Container: "matroska,webm",
                SizeBytes: 100,
                DurationSeconds: 60,
                Sha256: null,
                Ffprobe: null
            ),
            Encodes: [encode]
        );

    private static BlueprintEncode MakeEncode(string presetId, string? profileFingerprint) =>
        new(
            PresetSlug: "preset",
            PresetId: presetId,
            ProfileFingerprint: profileFingerprint,
            EncoderVersion: "3",
            TargetContainer: "matroska",
            OutputLocation: "Show/Show S01E01",
            CreatedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow,
            Tracks: [],
            ReconstructionCommandTemplate: "ffmpeg -c copy \"reconstructed.mkv\"",
            LossyWarnings: []
        );
}
