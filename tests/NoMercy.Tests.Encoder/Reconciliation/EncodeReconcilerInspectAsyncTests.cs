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
            "Show/Show S01E01",
            storage,
            CancellationToken.None
        );

        snapshot.BundleFiles.Should().BeEmpty();
        snapshot.ValidOcrSidecarCount.Should().Be(0);
        snapshot.ProfileFingerprint.Should().BeNull();
    }

    [Fact]
    public async Task InspectAsync_ListsFilesRelativeToTheMediaRoot_AndCountsOnlyValidOcrSidecars()
    {
        TestStorage storage = new();
        const string root = "Show/Show S01E01";
        storage.Seed($"{root}/web-1080p_master.m3u8", [0x01, 0x02]);
        storage.Seed($"{root}/subtitles/eng.ocr0.vtt", [0x01, 0x02, 0x03]);
        storage.Seed($"{root}/subtitles/eng.ocr1.vtt", []); // truncated — must not count
        storage.Seed($"{root}/subtitles/eng.vtt", [0x01]); // declared subtitle, not OCR

        ExistingOutputSnapshot snapshot = await _reconciler.InspectAsync(
            root,
            storage,
            CancellationToken.None
        );

        snapshot
            .BundleFiles.Select(f => f.RelativePath)
            .Should()
            .BeEquivalentTo(
                "web-1080p_master.m3u8",
                "subtitles/eng.ocr0.vtt",
                "subtitles/eng.ocr1.vtt",
                "subtitles/eng.vtt"
            );
        snapshot
            .ValidOcrSidecarCount.Should()
            .Be(1, "one OCR sidecar is truncated (zero bytes) and must not count as valid");
    }

    [Fact]
    public async Task InspectAsync_ReadsTheStoredProfileFingerprint_WhenManifestJsonIsPresent()
    {
        TestStorage storage = new();
        const string root = "Show/Show S01E01";
        BundleManifest manifest = new(
            Version: 1,
            EncoderVersion: "3",
            PresetId: "preset-id",
            PresetName: "preset",
            PresetSlug: "preset",
            MediaType: "episode",
            MediaId: 1,
            MediaExternalId: null,
            MediaFolder: "encodes/preset",
            Container: "hls-fmp4",
            CreatedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow,
            MediaKey: "abc123",
            Files: ["web-1080p_master.m3u8"],
            ProfileFingerprint: "deadbeef"
        );
        storage.Seed(
            $"{root}/manifest.json",
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest))
        );

        ExistingOutputSnapshot snapshot = await _reconciler.InspectAsync(
            root,
            storage,
            CancellationToken.None
        );

        snapshot.ProfileFingerprint.Should().Be("deadbeef");
    }

    [Fact]
    public async Task InspectAsync_ReturnsNullFingerprint_WhenManifestJsonHasNoneRecorded_LegacyOutput()
    {
        TestStorage storage = new();
        const string root = "Show/Show S01E01";
        BundleManifest legacyManifest = new(
            Version: 1,
            EncoderVersion: "3",
            PresetId: "preset-id",
            PresetName: "preset",
            PresetSlug: "preset",
            MediaType: "episode",
            MediaId: 1,
            MediaExternalId: null,
            MediaFolder: "encodes/preset",
            Container: "hls-fmp4",
            CreatedAt: DateTime.UtcNow,
            CompletedAt: DateTime.UtcNow,
            MediaKey: "abc123",
            Files: ["web-1080p_master.m3u8"]
        );
        storage.Seed(
            $"{root}/manifest.json",
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(legacyManifest))
        );

        ExistingOutputSnapshot snapshot = await _reconciler.InspectAsync(
            root,
            storage,
            CancellationToken.None
        );

        snapshot.ProfileFingerprint.Should().BeNull();
    }
}
