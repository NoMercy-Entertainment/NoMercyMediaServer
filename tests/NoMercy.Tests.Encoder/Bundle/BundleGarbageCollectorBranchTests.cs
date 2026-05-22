using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Bundle;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Bundle;

/// <summary>
/// Branch coverage for BundleGarbageCollector.SweepAsync — the filter
/// rules that decide which JSON files count as bundle manifests. The
/// rules are subtle and easy to regress: only files directly inside
/// {root}/encodes/{slug}/ qualify, sub-dirs are skipped, JSON files
/// outside /encodes/ are ignored.
/// </summary>
public class BundleGarbageCollectorBranchTests
{
    private const string LibraryRoot = "library";

    private static BundleManifest MakeManifest(string presetId, string presetSlug) =>
        new(
            Version: 1,
            EncoderVersion: "3.0.0",
            PresetId: presetId,
            PresetName: "Test Preset",
            PresetSlug: presetSlug,
            MediaType: "movie",
            MediaId: 550,
            MediaExternalId: null,
            MediaFolder: "/media/movies/Fight Club",
            Container: "hls-fmp4",
            CreatedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CompletedAt: new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            MediaKey: "mfa",
            Files: ["mfa_master.m3u8"]
        );

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
                new EncodingPreset
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
    )
    {
        BundleManifestWriter writer = new(storage);
        return new BundleGarbageCollector(
            storage,
            factory,
            writer,
            NullLogger<BundleGarbageCollector>.Instance
        );
    }

    [Fact]
    public async Task Sweep_JsonInBundleSubdirectory_IsIgnored()
    {
        // encodes/{slug}/video/foo.json must NOT count as a second manifest
        // — only files directly in the bundle root qualify. Without this
        // rule, any per-segment JSON written by a future plugin would
        // mass-flag bundles as duplicate-manifest orphans.
        const string presetId = "01HZTEST00000000000000B001";
        const string slug = "web-1080p";

        TestStorage storage = new();
        storage.Seed(
            $"{LibraryRoot}/encodes/{slug}/manifest.json",
            ToJsonBytes(MakeManifest(presetId, slug))
        );
        storage.Seed($"{LibraryRoot}/encodes/{slug}/mfa_master.m3u8", [0x01]);
        // JSON in a sub-directory of the bundle — must be ignored.
        storage.Seed($"{LibraryRoot}/encodes/{slug}/video/segment-meta.json", [0x02]);

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb(presetId);
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().BeEmpty("sub-directory JSON must not trigger duplicate-manifest");
    }

    [Fact]
    public async Task Sweep_JsonOutsideEncodesPath_IsIgnored()
    {
        // JSON files anywhere but under /encodes/ — config, cache, logs —
        // must never be considered as bundle manifests.
        TestStorage storage = new();
        storage.Seed($"{LibraryRoot}/cache/lookup.json", [0x01]);
        storage.Seed($"{LibraryRoot}/metadata/movie.json", [0x02]);
        storage.Seed($"{LibraryRoot}/some/deep/path/data.json", [0x03]);

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb();
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().BeEmpty("JSON outside /encodes/ is not a bundle manifest");
    }

    [Fact]
    public async Task Sweep_FilesPresentButNoJson_ReturnsNoOrphans()
    {
        // Encodes tree exists but no manifest has been written yet —
        // mid-encode state. The sweep must not invent orphans from
        // partial segments.
        TestStorage storage = new();
        storage.Seed($"{LibraryRoot}/encodes/web-1080p/mfa_master.m3u8", [0x01]);
        storage.Seed($"{LibraryRoot}/encodes/web-1080p/video/1080p/mfa_1080p_init.mp4", [0x02]);
        storage.Seed($"{LibraryRoot}/encodes/web-1080p/video/1080p/mfa_1080p_00001.m4s", [0x03]);

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
        // libraryRoot is user-provided — both "library" and "library/"
        // must yield the same sweep result. The source TrimEnd's the
        // slash; verify here so a future refactor doesn't drop it.
        const string slug = "web-4k";

        TestStorage storage = new();
        storage.Seed(
            $"{LibraryRoot}/encodes/{slug}/manifest.json",
            ToJsonBytes(MakeManifest("01HZTEST00000000000000B002", slug))
        );
        storage.Seed($"{LibraryRoot}/encodes/{slug}/mfa_master.m3u8", [0x01]);

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
    public async Task Sweep_MultipleBundles_ProcessesEachIndependently()
    {
        // Three bundles: one healthy, one with deleted preset, one with
        // duplicate manifest. The sweep must return exactly the two
        // orphans without skipping the third bundle on the first miss.
        const string aliveId = "01HZTEST00000000000000B003";
        const string deadId = "01HZTEST00000000000000B004";

        TestStorage storage = new();

        // Healthy bundle.
        storage.Seed(
            $"{LibraryRoot}/encodes/alive/manifest.json",
            ToJsonBytes(MakeManifest(aliveId, "alive"))
        );
        storage.Seed($"{LibraryRoot}/encodes/alive/mfa_master.m3u8", [0x01]);

        // Deleted-preset bundle.
        storage.Seed(
            $"{LibraryRoot}/encodes/dead/manifest.json",
            ToJsonBytes(MakeManifest(deadId, "dead"))
        );

        // Duplicate-manifest bundle.
        storage.Seed(
            $"{LibraryRoot}/encodes/dup/manifest.json",
            ToJsonBytes(MakeManifest(aliveId, "dup"))
        );
        storage.Seed(
            $"{LibraryRoot}/encodes/dup/manifest.backup.json",
            ToJsonBytes(MakeManifest(aliveId, "dup"))
        );

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb(aliveId);
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().HaveCount(2);
        orphans.Should().Contain(o => o.PresetSlug == "dead" && o.Reason == "preset deleted");
        orphans.Should().Contain(o => o.PresetSlug == "dup" && o.Reason == "duplicate-manifest");
    }

    [Fact]
    public async Task Sweep_CancellationRequested_PropagatesViaContextFactory()
    {
        // The factory passes the token through to EF Core; if the caller
        // cancels before any DB work the sweep should observe it.
        const string presetId = "01HZTEST00000000000000B005";

        TestStorage storage = new();
        storage.Seed(
            $"{LibraryRoot}/encodes/web-1080p/manifest.json",
            ToJsonBytes(MakeManifest(presetId, "web-1080p"))
        );

        Mock<IDbContextFactory<MediaContext>> factoryMock = new();
        factoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        BundleManifestWriter writer = new(storage);
        BundleGarbageCollector gc = new(
            storage,
            factoryMock.Object,
            writer,
            NullLogger<BundleGarbageCollector>.Instance
        );

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => gc.SweepAsync(LibraryRoot, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
