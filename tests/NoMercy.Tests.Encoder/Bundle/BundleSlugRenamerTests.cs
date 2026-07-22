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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Encoder.Bundle;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Bundle;

// ---------------------------------------------------------------------------
// Fake IStorageFactory that always returns the same storage instance
// ---------------------------------------------------------------------------

internal sealed class FixedStorageFactory(IStorage storage) : IStorageFactory
{
    public IStorage For(Ulid folderId, Ulid driverId, string subPath) => storage;

    public void Invalidate(Ulid folderId) { }

    public void InvalidateAll() { }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

internal static class RenameTestHelpers
{
    private static readonly Ulid FolderId = Ulid.NewUlid();
    private static readonly Ulid DriverId = Ulid.NewUlid();

    public static MediaContext BuildInMemoryContext(string folderPath = "/fake/library")
    {
        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase(databaseName: Ulid.NewUlid().ToString())
            .Options;

        MediaContext ctx = new(options: opts);

        ctx.Drivers.Add(
            entity: new()
            {
                Id = DriverId,
                Name = "local",
                Type = "local",
                Config = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );

        ctx.Folders.Add(
            entity: new()
            {
                Id = FolderId,
                Path = folderPath,
                DriverId = DriverId,
            }
        );

        ctx.SaveChanges();
        return ctx;
    }

    /// <summary>Builds a minimal-but-realistic <c>.nomercy.json</c> blueprint
    /// with one <c>encodes[]</c> entry per given preset slug.</summary>
    public static string BuildBlueprintJson(params string[] presetSlugs) =>
        JsonConvert.SerializeObject(
            value: new JObject
            {
                [propertyName: "version"] = 1,
                [propertyName: "identity"] = new JObject { [propertyName: "type"] = "movie", [propertyName: "tmdb_id"] = 550 },
                [propertyName: "source"] = new JObject { [propertyName: "path"] = "Fight Club.mkv" },
                [propertyName: "encodes"] = new JArray(
                    content: presetSlugs.Select(selector: slug => new JObject
                    {
                        [propertyName: "preset_slug"] = slug,
                        [propertyName: "preset_id"] = "01J3X8R7K2QM9Y0G1Q4ABCDEFG",
                        [propertyName: "profile_fingerprint"] = "abc123",
                    })
                ),
            },
            formatting: Formatting.Indented
        );

    public static BundleSlugRenamer BuildRenamer(
        Dictionary<string, string> slugMap,
        IStorage storage,
        MediaContext context
    )
    {
        FixedStorageFactory factory = new(storage: storage);
        return new(
            slugMap: slugMap,
            storageFactory: factory,
            context: context,
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<BundleSlugRenamer>.Instance
        );
    }

    public static JObject ReadBlueprint(TestStorage storage, string path)
    {
        string? json = storage.ReadString(path: path);
        json.Should().NotBeNull();
        return JsonConvert.DeserializeObject<JObject>(value: json!)!;
    }

    public static IReadOnlyList<string> EncodeSlugs(JObject blueprint) =>
        [.. ((JArray)blueprint[propertyName: "encodes"]!).Select(selector: e => e[key: "preset_slug"]!.Value<string>()!)];
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class BundleSlugRenamerTests
{
    [Fact]
    public async Task HappyPath_RewritesPresetSlugInBlueprint()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Show/Show S01E01/.nomercy.json";
        storage.Seed(
            path: path,
            bytes: Encoding.UTF8.GetBytes(s: RenameTestHelpers.BuildBlueprintJson(presetSlugs: "old-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            slugMap: new() { [key: "old-slug"] = "new-slug" },
            storage: storage,
            context: context
        );

        await renamer.RunAsync();

        // The file stays at the same path — there is no directory to move.
        storage.AllPaths().Should().Contain(expected: path);

        JObject blueprint = RenameTestHelpers.ReadBlueprint(storage: storage, path: path);
        RenameTestHelpers
            .EncodeSlugs(blueprint: blueprint)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(expected: "new-slug");
    }

    [Fact]
    public async Task Idempotent_SecondRunIsNoOp()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Show/Show S01E01/.nomercy.json";
        storage.Seed(
            path: path,
            bytes: Encoding.UTF8.GetBytes(s: RenameTestHelpers.BuildBlueprintJson(presetSlugs: "old-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            slugMap: new() { [key: "old-slug"] = "new-slug" },
            storage: storage,
            context: context
        );

        // First run rewrites old-slug → new-slug.
        await renamer.RunAsync();
        string? afterFirst = storage.ReadString(path: path);

        // Second run: the entry now reads "new-slug", which no longer
        // matches the map's old-slug key, so nothing changes.
        await renamer.RunAsync();
        string? afterSecond = storage.ReadString(path: path);

        afterSecond.Should().Be(expected: afterFirst);
        RenameTestHelpers
            .EncodeSlugs(blueprint: RenameTestHelpers.ReadBlueprint(storage: storage, path: path))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(expected: "new-slug");
    }

    [Fact]
    public async Task UnrelatedSlug_IsNotRewritten()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Show/Show S01E01/.nomercy.json";
        storage.Seed(
            path: path,
            bytes: Encoding.UTF8.GetBytes(s: RenameTestHelpers.BuildBlueprintJson(presetSlugs: "totally-unrelated"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            slugMap: new() { [key: "old-slug"] = "new-slug" },
            storage: storage,
            context: context
        );

        await renamer.RunAsync();

        RenameTestHelpers
            .EncodeSlugs(blueprint: RenameTestHelpers.ReadBlueprint(storage: storage, path: path))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(expected: "totally-unrelated");
    }

    [Fact]
    public async Task EmptySlugMap_IsNoOp()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Show/Show S01E01/.nomercy.json";
        string original = RenameTestHelpers.BuildBlueprintJson(presetSlugs: "some-slug");
        storage.Seed(path: path, bytes: Encoding.UTF8.GetBytes(s: original));

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(slugMap: new(), storage: storage, context: context);

        await renamer.RunAsync();

        storage.ReadString(path: path).Should().Be(expected: original);
    }
}
