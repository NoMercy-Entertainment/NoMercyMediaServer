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
using NoMercy.Encoder.Bundle;

namespace NoMercy.Tests.Encoder.Bundle;

/// <summary>
/// Branch coverage for BundleGarbageCollector.SweepAsync — the filter rule
/// that decides which files count as a per-media-item blueprint (only a
/// file literally named <c>.nomercy.json</c>, anywhere under the library
/// root) plus the DB-cancellation and multi-item paths.
/// </summary>
public class BundleGarbageCollectorBranchTests
{
    private const string LibraryRoot = "library";

    private static object MakeEncode(string presetId, string presetSlug) =>
        new
        {
            preset_slug = presetSlug,
            preset_id = presetId,
            profile_fingerprint = "abc123",
            encoder_version = "3.0.0",
            target_container = "matroska",
            output_location = "",
            created_at = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            completed_at = new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            tracks = Array.Empty<object>(),
            reconstruction_command_template = "ffmpeg -i in -c copy out.mkv",
            lossy_warnings = Array.Empty<string>(),
        };

    private static object MakeBlueprint(params object[] encodes) =>
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
        Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));

    private static (MediaContext context, IDbContextFactory<MediaContext> factory) MakeDb(
        params string[] presetIds
    )
    {
        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase($"gc-branch-{Guid.NewGuid()}")
            .Options;
        MediaContext ctx = new(opts);
        foreach (string id in presetIds)
        {
            ctx.EncodingPresets.Add(
                new()
                {
                    Id = Ulid.Parse(id),
                    Name = $"Preset {id}",
                    ProfileJson = "{}",
                }
            );
        }
        ctx.SaveChanges();

        Mock<IDbContextFactory<MediaContext>> factoryMock = new();
        factoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ctx);
        return (ctx, factoryMock.Object);
    }

    private static BundleGarbageCollector MakeCollector(
        TestStorage storage,
        IDbContextFactory<MediaContext> factory
    ) => new(storage, factory, NullLogger<BundleGarbageCollector>.Instance);

    [Fact]
    public async Task Sweep_JsonNotNamedNomercyJson_IsIgnored()
    {
        // Any JSON file that isn't literally ".nomercy.json" — cache, config,
        // metadata sidecars — must never be read as a blueprint.
        TestStorage storage = new();
        storage.Seed($"{LibraryRoot}/cache/lookup.json", [0x01]);
        storage.Seed($"{LibraryRoot}/Movie/metadata.json", [0x02]);
        storage.Seed(
            $"{LibraryRoot}/Movie/encodes/web-1080p/manifest.json",
            ToJsonBytes(MakeBlueprint(MakeEncode("01HZTEST00000000000000B001", "web-1080p")))
        );

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb();
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().BeEmpty("only a file literally named .nomercy.json is a blueprint");
    }

    [Fact]
    public async Task Sweep_RenditionFilesPresentButNoBlueprint_ReturnsNoOrphans()
    {
        // Renditions exist at the media root but the encode hasn't finalized
        // yet (no .nomercy.json written) — mid-encode state, not an orphan.
        TestStorage storage = new();
        storage.Seed($"{LibraryRoot}/Movie/mfa_master.m3u8", [0x01]);
        storage.Seed($"{LibraryRoot}/Movie/video_1080p/mfa_1080p_init.mp4", [0x02]);

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb();
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_LibraryRootWithTrailingSlash_BehavesSameAsWithoutSlash()
    {
        const string mediaFolder = "web-4k-item";

        TestStorage storage = new();
        storage.Seed(
            $"{LibraryRoot}/{mediaFolder}/.nomercy.json",
            ToJsonBytes(MakeBlueprint(MakeEncode("01HZTEST00000000000000B002", "web-4k")))
        );

        // Preset is missing from DB → expect a "preset deleted" orphan
        // whether the trailing slash is on or off. Each sweep needs its
        // own factory: SweepAsync disposes the context, so a shared
        // factory mock would hand out a dead instance on the second call.
        (MediaContext _, IDbContextFactory<MediaContext> factoryA) = MakeDb();
        (MediaContext _, IDbContextFactory<MediaContext> factoryB) = MakeDb();

        IReadOnlyList<BundleOrphan> withSlash = await MakeCollector(storage, factoryA)
            .SweepAsync(LibraryRoot + "/", CancellationToken.None);
        IReadOnlyList<BundleOrphan> withoutSlash = await MakeCollector(storage, factoryB)
            .SweepAsync(LibraryRoot, CancellationToken.None);

        withSlash.Should().ContainSingle().Which.Reason.Should().Be("preset deleted");
        withoutSlash.Should().ContainSingle().Which.Reason.Should().Be("preset deleted");
    }

    [Fact]
    public async Task Sweep_MultipleMediaItems_ProcessesEachIndependently()
    {
        // Three media items: one healthy, one with a deleted preset, one
        // with a mix of both. The sweep must return exactly the two orphan
        // entries without skipping the third item on the first miss.
        const string aliveId = "01HZTEST00000000000000B003";
        const string deadId = "01HZTEST00000000000000B004";

        TestStorage storage = new();

        storage.Seed(
            $"{LibraryRoot}/Alive Movie/.nomercy.json",
            ToJsonBytes(MakeBlueprint(MakeEncode(aliveId, "alive")))
        );
        storage.Seed(
            $"{LibraryRoot}/Dead Movie/.nomercy.json",
            ToJsonBytes(MakeBlueprint(MakeEncode(deadId, "dead")))
        );
        storage.Seed(
            $"{LibraryRoot}/Mixed Movie/.nomercy.json",
            ToJsonBytes(
                MakeBlueprint([MakeEncode(aliveId, "alive-again"), MakeEncode(deadId, "dead-again")])
            )
        );

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb(aliveId);
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().HaveCount(2);
        orphans.Should().Contain(o => o.PresetSlug == "dead" && o.Reason == "preset deleted");
        orphans.Should().Contain(o => o.PresetSlug == "dead-again" && o.Reason == "preset deleted");
    }

    [Fact]
    public async Task Sweep_CancellationRequested_PropagatesViaContextFactory()
    {
        // The factory passes the token through to EF Core; if the caller
        // cancels before any DB work the sweep should observe it.
        TestStorage storage = new();
        storage.Seed(
            $"{LibraryRoot}/Movie/.nomercy.json",
            ToJsonBytes(MakeBlueprint(MakeEncode("01HZTEST00000000000000B005", "web-1080p")))
        );

        Mock<IDbContextFactory<MediaContext>> factoryMock = new();
        factoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        BundleGarbageCollector gc = new(
            storage,
            factoryMock.Object,
            NullLogger<BundleGarbageCollector>.Instance
        );

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => gc.SweepAsync(LibraryRoot, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
