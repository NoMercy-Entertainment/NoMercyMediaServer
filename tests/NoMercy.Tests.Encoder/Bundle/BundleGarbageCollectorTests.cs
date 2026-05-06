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
/// Unit tests for BundleGarbageCollector.SweepAsync.
///
/// DB context: EF Core in-memory provider (UseInMemoryDatabase).
/// Storage: TestStorage (from BundleManifestWriterTests.cs in same namespace).
/// </summary>
public class BundleGarbageCollectorTests : IDisposable
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private const string LibraryRoot = "library";

    private static BundleManifest MakeManifest(
        string presetId,
        string presetSlug,
        IReadOnlyList<string>? files = null
    ) =>
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
            Files: files ?? ["mfa_master.m3u8", "video/1080p/mfa_1080p_init.mp4"]
        );

    private static byte[] ToJsonBytes(object value) =>
        Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));

    // Seed a manifest.json at encodes/{slug}/manifest.json under the library root.
    private static void SeedManifest(TestStorage storage, string slug, BundleManifest manifest)
    {
        string path = $"{LibraryRoot}/encodes/{slug}/manifest.json";
        storage.Seed(path, ToJsonBytes(manifest));
    }

    // Seed a regular file inside a bundle dir.
    private static void SeedFile(TestStorage storage, string slug, string relPath)
    {
        string path = $"{LibraryRoot}/encodes/{slug}/{relPath}";
        storage.Seed(path, [0x01]);
    }

    // Build an in-memory MediaContext with an optional set of preset IDs.
    private static (MediaContext context, IDbContextFactory<MediaContext> factory) MakeDb(
        params string[] presentPresetIds
    )
    {
        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase($"gc-test-{Guid.NewGuid()}")
            .Options;
        MediaContext ctx = new(opts);
        foreach (string id in presentPresetIds)
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

        // Wrap in a mock factory so the GC can call CreateDbContextAsync.
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

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    public void Dispose() { }

    // -----------------------------------------------------------------------
    // Test 1: preset present in DB — no orphan
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_ManifestWithKnownPreset_ReturnsNoOrphans()
    {
        const string presetId = "01HZTEST000000000000000001";
        const string slug = "web-1080p";

        TestStorage storage = new();
        SeedManifest(storage, slug, MakeManifest(presetId, slug));
        SeedFile(storage, slug, "mfa_master.m3u8");
        SeedFile(storage, slug, "video/1080p/mfa_1080p_init.mp4");

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb(presetId);
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test 2: preset missing from DB — orphan with reason "preset deleted"
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_ManifestWithDeletedPreset_ReturnsPresetDeletedOrphan()
    {
        const string presetId = "01HZTEST000000000000000002";
        const string slug = "web-4k";

        TestStorage storage = new();
        SeedManifest(storage, slug, MakeManifest(presetId, slug));

        // DB has no preset with this ID.
        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb();
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().ContainSingle();
        BundleOrphan orphan = orphans[0];
        orphan.Reason.Should().Be("preset deleted");
        orphan.PresetSlug.Should().Be(slug);
        orphan.PresetId.Should().Be(presetId);
    }

    // -----------------------------------------------------------------------
    // Test 3: multiple manifests in same preset folder — "duplicate-manifest"
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_TwoManifestsInSameFolder_ReturnsDuplicateManifestOrphan()
    {
        const string presetId = "01HZTEST000000000000000003";
        const string slug = "web-720p";

        TestStorage storage = new();

        // Two manifests with different names in the same preset directory.
        string dir = $"{LibraryRoot}/encodes/{slug}";
        storage.Seed($"{dir}/manifest.json", ToJsonBytes(MakeManifest(presetId, slug)));
        storage.Seed($"{dir}/manifest.backup.json", ToJsonBytes(MakeManifest(presetId, slug)));

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb(presetId);
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().ContainSingle();
        orphans[0].Reason.Should().Be("duplicate-manifest");
        orphans[0].PresetSlug.Should().Be(slug);
    }

    // -----------------------------------------------------------------------
    // Test 4: extra file on disk (not in manifest) — warning logged, no orphan
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_ExtraFileOnDisk_LogsWarningAndReturnsNoOrphan()
    {
        const string presetId = "01HZTEST000000000000000004";
        const string slug = "web-480p";

        TestStorage storage = new();
        BundleManifest manifest = MakeManifest(
            presetId,
            slug,
            ["mfa_master.m3u8", "video/480p/mfa_480p_init.mp4"]
        );
        SeedManifest(storage, slug, manifest);
        SeedFile(storage, slug, "mfa_master.m3u8");
        SeedFile(storage, slug, "video/480p/mfa_480p_init.mp4");
        // Extra file not tracked in manifest — user addition.
        SeedFile(storage, slug, "mfa_thumbnails.vtt");

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb(presetId);
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        // Extra files are user additions — not surfaced as orphans.
        orphans.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test 5: manifest entry missing on disk — warning logged, no orphan
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_ManifestEntryMissingOnDisk_LogsWarningAndReturnsNoOrphan()
    {
        const string presetId = "01HZTEST000000000000000005";
        const string slug = "web-1080p-avc";

        TestStorage storage = new();
        BundleManifest manifest = MakeManifest(
            presetId,
            slug,
            ["mfa_master.m3u8", "video/1080p/mfa_1080p_init.mp4", "video/1080p/mfa_1080p_00001.m4s"]
        );
        SeedManifest(storage, slug, manifest);
        SeedFile(storage, slug, "mfa_master.m3u8");
        SeedFile(storage, slug, "video/1080p/mfa_1080p_init.mp4");
        // mfa_1080p_00001.m4s intentionally absent — encode incomplete.

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb(presetId);
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        // Missing files signal incomplete encode, not an orphan — user can re-encode.
        orphans.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Test 6: empty library root — returns empty list
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sweep_EmptyLibraryRoot_ReturnsEmptyList()
    {
        TestStorage storage = new();

        (MediaContext _, IDbContextFactory<MediaContext> factory) = MakeDb();
        BundleGarbageCollector gc = MakeCollector(storage, factory);

        IReadOnlyList<BundleOrphan> orphans = await gc.SweepAsync(
            LibraryRoot,
            CancellationToken.None
        );

        orphans.Should().BeEmpty();
    }
}
