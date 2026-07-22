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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Bundle;

namespace NoMercy.Tests.Encoder.Bundle;

/// <summary>
/// Unit tests for BundleGarbageCollector.SweepAsync against the unified
/// per-media-item <c>.nomercy.json</c> blueprint (replaces the old
/// per-preset <c>encodes/{slug}/manifest.json</c> sweep).
///
/// DB context: EF Core in-memory provider (UseInMemoryDatabase).
/// Storage: TestStorage (shared fake, see TestStorage.cs).
/// </summary>
public class BundleGarbageCollectorTests
{
    private const string LibraryRoot = "library";

    private static object MakeEncode(string presetId, string presetSlug, string outputLocation) =>
        new
        {
            preset_slug = presetSlug,
            preset_id = presetId,
            profile_fingerprint = "abc123",
            encoder_version = "3.0.0",
            target_container = "matroska",
            output_location = outputLocation,
            created_at = new DateTime(year: 2026, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc),
            completed_at = new DateTime(year: 2026, month: 1, day: 1, hour: 1, minute: 0, second: 0, kind: DateTimeKind.Utc),
            tracks = Array.Empty<object>(),
            reconstruction_command_template = "ffmpeg -i in -c copy out.mkv",
            lossy_warnings = Array.Empty<string>(),
        };

    private static object MakeBlueprint(string mediaFolder, params object[] encodes) =>
        new
        {
            version = 1,
            identity = new
            {
                type = "movie",
                tmdb_id = 550,
                title = "Fight Club",
                year = 1999,
            },
            source = new
            {
                path = "Download/complete/Fight Club.mkv",
                filename = "Fight Club.mkv",
                container = "matroska,webm",
                size_bytes = 1_500_000_000L,
                duration_seconds = 8000.0,
                sha256 = (string?)null,
                ffprobe = new { },
            },
            encodes,
        };

    private static byte[] ToJsonBytes(object value) =>
        Encoding.UTF8.GetBytes(s: JsonConvert.SerializeObject(value: value));

    // Seed a .nomercy.json at {mediaFolder}/.nomercy.json under the library root.
    private static void SeedBlueprint(
        TestStorage storage,
        string mediaFolder,
        params object[] encodes
    )
    {
        string path = $"{LibraryRoot}/{mediaFolder}/.nomercy.json";
        storage.Seed(path: path, bytes: ToJsonBytes(value: MakeBlueprint(mediaFolder: mediaFolder, encodes: encodes)));
    }

    // Build an in-memory MediaContext with an optional set of preset IDs.
    private static (MediaContext context, IDbContextFactory<MediaContext> factory) MakeDb(
        params string[] presentPresetIds
    )
    {
        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase(databaseName: $"gc-test-{Guid.NewGuid()}")
            .Options;
        MediaContext ctx = new(options: opts);
        foreach (string id in presentPresetIds)
        {
            ctx.EncodingPresets.Add(
                entity: new()
                {
                    Id = Ulid.Parse(base32: id),
                    Name = $"Preset {id}",
                    ProfileJson = "{}",
                }
            );
        }
        ctx.SaveChanges();

        // Wrap in a mock factory so the GC can call CreateDbContextAsync.
        Mock<IDbContextFactory<MediaContext>> factoryMock = new();
        factoryMock
            .Setup(expression: f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: ctx);
        return (ctx, factoryMock.Object);
    }

    private static BundleGarbageCollector MakeCollector(
        TestStorage storage,
        IDbContextFactory<MediaContext> factory
    ) => new(storage: storage, contextFactory: factory, logger: NullLogger<BundleGarbageCollector>.Instance);

    // -----------------------------------------------------------------------
    // Test 1: preset present in DB — no orphan
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_BlueprintWithKnownPreset_ReturnsNoOrphans()
    {
        const string presetId = "01HZTEST000000000000000001";
        const string mediaFolder = "Fight Club (1999)";

        TestStorage storage = new();
        SeedBlueprint(
            storage: storage,
            mediaFolder: mediaFolder,
            encodes: MakeEncode(presetId: presetId, presetSlug: "web-1080p", outputLocation: $"{LibraryRoot}/{mediaFolder}")
        );

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb(presentPresetIds: presetId);
        BundleGarbageCollector gc = MakeCollector(storage: storage, factory: factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            libraryRoot: LibraryRoot,
            ct: CancellationToken.None
        );

        orphans.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test 2: preset missing from DB — orphan with reason "preset deleted"
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_BlueprintWithDeletedPreset_ReturnsPresetDeletedOrphan()
    {
        const string presetId = "01HZTEST000000000000000002";
        const string mediaFolder = "Se7en (1995)";
        string outputLocation = $"{LibraryRoot}/{mediaFolder}";

        TestStorage storage = new();
        SeedBlueprint(storage: storage, mediaFolder: mediaFolder, encodes: MakeEncode(presetId: presetId, presetSlug: "web-4k", outputLocation: outputLocation));

        // DB has no preset with this ID.
        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb();
        BundleGarbageCollector gc = MakeCollector(storage: storage, factory: factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            libraryRoot: LibraryRoot,
            ct: CancellationToken.None
        );

        orphans.Should().ContainSingle();
        BundleOrphan orphan = orphans[index: 0];
        orphan.Reason.Should().Be(expected: "preset deleted");
        orphan.PresetSlug.Should().Be(expected: "web-4k");
        orphan.PresetId.Should().Be(expected: presetId);
        orphan.Path.Should().Be(expected: outputLocation);
    }

    // -----------------------------------------------------------------------
    // Test 3: multiple encode entries in one blueprint — only the dead one
    // is orphaned, the live one is not double-counted or dropped.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_BlueprintWithMixedPresets_OnlyOrphansDeletedPreset()
    {
        const string aliveId = "01HZTEST000000000000000003";
        const string deadId = "01HZTEST000000000000000004";
        const string mediaFolder = "Interstellar (2014)";

        TestStorage storage = new();
        SeedBlueprint(
            storage: storage,
            mediaFolder: mediaFolder, encodes: [MakeEncode(presetId: aliveId, presetSlug: "web-1080p", outputLocation: $"{LibraryRoot}/{mediaFolder}"), MakeEncode(presetId: deadId, presetSlug: "web-720p-legacy", outputLocation: $"{LibraryRoot}/{mediaFolder}")]
        );

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb(presentPresetIds: aliveId);
        BundleGarbageCollector gc = MakeCollector(storage: storage, factory: factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            libraryRoot: LibraryRoot,
            ct: CancellationToken.None
        );

        orphans.Should().ContainSingle();
        orphans[index: 0].PresetSlug.Should().Be(expected: "web-720p-legacy");
        orphans[index: 0].PresetId.Should().Be(expected: deadId);
    }

    // -----------------------------------------------------------------------
    // Test 4: a leftover pre-blueprint encodes/{slug}/manifest.json must not
    // crash the sweep and must not be double-counted as an orphan.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_VestigialLegacyManifestPresent_DoesNotCrashAndIsNotCounted()
    {
        const string presetId = "01HZTEST000000000000000005";
        const string mediaFolder = "The Matrix (1999)";
        string outputLocation = $"{LibraryRoot}/{mediaFolder}";

        TestStorage storage = new();
        SeedBlueprint(storage: storage, mediaFolder: mediaFolder, encodes: MakeEncode(presetId: presetId, presetSlug: "web-1080p", outputLocation: outputLocation));

        // Vestigial old-layout file left behind by a pre-migration install —
        // never inspected because its filename isn't ".nomercy.json".
        storage.Seed(
            path: $"{LibraryRoot}/{mediaFolder}/encodes/web-1080p/manifest.json",
            bytes: Encoding.UTF8.GetBytes(s: "{\"preset_slug\":\"web-1080p\"}")
        );

        // Preset is missing from DB — the blueprint entry (not the legacy
        // file) is the one that must produce exactly one orphan.
        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb();
        BundleGarbageCollector gc = MakeCollector(storage: storage, factory: factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            libraryRoot: LibraryRoot,
            ct: CancellationToken.None
        );

        orphans.Should().ContainSingle();
        orphans[index: 0].Reason.Should().Be(expected: "preset deleted");
        orphans[index: 0].PresetSlug.Should().Be(expected: "web-1080p");
    }

    // -----------------------------------------------------------------------
    // Test 5: empty library root — returns empty list
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_EmptyLibraryRoot_ReturnsEmptyList()
    {
        TestStorage storage = new();

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb();
        BundleGarbageCollector gc = MakeCollector(storage: storage, factory: factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            libraryRoot: LibraryRoot,
            ct: CancellationToken.None
        );

        orphans.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test 6: unreadable / malformed blueprint is skipped, not thrown
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_MalformedBlueprintJson_IsSkippedWithoutThrowing()
    {
        const string mediaFolder = "Corrupt Item";

        TestStorage storage = new();
        storage.Seed(
            path: $"{LibraryRoot}/{mediaFolder}/.nomercy.json",
            bytes: Encoding.UTF8.GetBytes(s: "not valid json")
        );

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb();
        BundleGarbageCollector gc = MakeCollector(storage: storage, factory: factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            libraryRoot: LibraryRoot,
            ct: CancellationToken.None
        );

        orphans.Should().BeEmpty();
    }
}
