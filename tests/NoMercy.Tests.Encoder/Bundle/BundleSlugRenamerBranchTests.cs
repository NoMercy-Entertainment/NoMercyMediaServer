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
using Newtonsoft.Json.Linq;
using NoMercy.Database;
using NoMercy.Encoder.Bundle;

namespace NoMercy.Tests.Encoder.Bundle;

/// <summary>
/// Branch-coverage gaps for <see cref="BundleSlugRenamer"/> beyond the
/// happy-path / idempotent / unrelated-slug / empty-map cases already
/// covered in <see cref="BundleSlugRenamerTests"/>:
///
/// • Multiple slug pairs in a single run, and multiple encode entries in a
///   single blueprint — each rewritten independently.
/// • Malformed blueprint JSON — logged and skipped, does not block other
///   blueprints in the same sweep.
/// • Blueprint with no <c>encodes</c> array — skipped silently.
/// • Extra blueprint fields (identity/source/other encode fields) round-trip
///   untouched; only the matching entry's <c>preset_slug</c> changes.
/// • Empty/whitespace old or new slug in the map — guarded, no rewrite, no throw.
/// • Multiple library folders processed independently.
/// </summary>
public class BundleSlugRenamerBranchTests
{
    [Fact]
    public async Task Multiple_pairs_and_multiple_encode_entries_each_rewritten_independently()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Show/Show S01E01/.nomercy.json";
        storage.Seed(
            path: path,
            bytes: Encoding.UTF8.GetBytes(
                s: RenameTestHelpers.BuildBlueprintJson(presetSlugs: ["preset-a-old", "preset-b-old", "untouched"])
            )
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            slugMap: new() { [key: "preset-a-old"] = "preset-a-new", [key: "preset-b-old"] = "preset-b-new" },
            storage: storage,
            context: context
        );

        await renamer.RunAsync();

        IReadOnlyList<string> slugs = RenameTestHelpers.EncodeSlugs(
            blueprint: RenameTestHelpers.ReadBlueprint(storage: storage, path: path)
        );
        slugs.Should().BeEquivalentTo(expectation: ["preset-a-new", "preset-b-new", "untouched"]);
    }

    [Fact]
    public async Task Malformed_blueprint_json_is_skipped_and_does_not_block_other_items()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string corruptPath = "Corrupt Item/.nomercy.json";
        const string validPath = "Good Item/.nomercy.json";

        storage.Seed(path: corruptPath, bytes: Encoding.UTF8.GetBytes(s: "not valid json"));
        storage.Seed(
            path: validPath,
            bytes: Encoding.UTF8.GetBytes(s: RenameTestHelpers.BuildBlueprintJson(presetSlugs: "old-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            slugMap: new() { [key: "old-slug"] = "new-slug" },
            storage: storage,
            context: context
        );

        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        // Corrupt file is left untouched for forensic recovery.
        storage.ReadString(path: corruptPath).Should().Be(expected: "not valid json");

        RenameTestHelpers
            .EncodeSlugs(blueprint: RenameTestHelpers.ReadBlueprint(storage: storage, path: validPath))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(expected: "new-slug");
    }

    [Fact]
    public async Task Blueprint_without_encodes_array_is_skipped_without_throwing()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Legacy Item/.nomercy.json";
        string legacyJson = JsonConvert.SerializeObject(
            value: new JObject
            {
                [propertyName: "version"] = 1,
                [propertyName: "identity"] = new JObject { [propertyName: "type"] = "movie" },
            }
        );
        storage.Seed(path: path, bytes: Encoding.UTF8.GetBytes(s: legacyJson));

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            slugMap: new() { [key: "old-slug"] = "new-slug" },
            storage: storage,
            context: context
        );

        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        storage.ReadString(path: path).Should().Be(expected: legacyJson);
    }

    [Fact]
    public async Task Extra_blueprint_fields_and_unmatched_encode_fields_round_trip_untouched()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Fight Club (1999)/.nomercy.json";
        JObject original = new()
        {
            [propertyName: "version"] = 1,
            [propertyName: "identity"] = new JObject
            {
                [propertyName: "type"] = "movie",
                [propertyName: "tmdb_id"] = 550,
                [propertyName: "title"] = "Fight Club",
            },
            [propertyName: "source"] = new JObject { [propertyName: "path"] = "Fight Club.mkv" },
            [propertyName: "encodes"] = new JArray(
                content: new JObject
                {
                    [propertyName: "preset_slug"] = "old-slug",
                    [propertyName: "preset_id"] = "01J3X8R7K2QM9Y0G1Q4ABCDEFG",
                    [propertyName: "profile_fingerprint"] = "abc123",
                    [propertyName: "custom_field"] = "preserved",
                }
            ),
        };
        storage.Seed(path: path, bytes: Encoding.UTF8.GetBytes(s: JsonConvert.SerializeObject(value: original)));

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            slugMap: new() { [key: "old-slug"] = "new-slug" },
            storage: storage,
            context: context
        );

        await renamer.RunAsync();

        JObject patched = RenameTestHelpers.ReadBlueprint(storage: storage, path: path);
        JObject encode = (JObject)patched[propertyName: "encodes"]![key: 0]!;

        encode[propertyName: "preset_slug"]!.Value<string>().Should().Be(expected: "new-slug");
        encode[propertyName: "preset_id"]!.Value<string>().Should().Be(expected: "01J3X8R7K2QM9Y0G1Q4ABCDEFG");
        encode[propertyName: "profile_fingerprint"]!.Value<string>().Should().Be(expected: "abc123");
        encode[propertyName: "custom_field"]!.Value<string>().Should().Be(expected: "preserved");
        patched[propertyName: "version"]!.Value<int>().Should().Be(expected: 1);
        patched[propertyName: "identity"]![key: "title"]!.Value<string>().Should().Be(expected: "Fight Club");
        patched[propertyName: "source"]![key: "path"]!.Value<string>().Should().Be(expected: "Fight Club.mkv");
    }

    [Theory]
    [InlineData(data: ["", "valid-new"])]
    [InlineData(data: ["valid-old", ""])]
    [InlineData(data: ["   ", "valid-new"])]
    [InlineData(data: ["valid-old", "   "])]
    public async Task Empty_or_whitespace_slug_in_pair_skipped_to_prevent_mass_rewrite(
        string oldSlug,
        string newSlug
    )
    {
        // Both ends of the pair must be non-empty/non-whitespace — an empty
        // key/value would either match every blank slug or rewrite entries
        // to a meaningless value. This guard pins the defensive check.
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Real Item/.nomercy.json";
        storage.Seed(
            path: path,
            bytes: Encoding.UTF8.GetBytes(s: RenameTestHelpers.BuildBlueprintJson(presetSlugs: "real-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            slugMap: new() { [key: oldSlug] = newSlug },
            storage: storage,
            context: context
        );

        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        RenameTestHelpers
            .EncodeSlugs(blueprint: RenameTestHelpers.ReadBlueprint(storage: storage, path: path))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(expected: "real-slug");
    }

    [Fact]
    public async Task Multiple_library_folders_processed_independently()
    {
        // Note: BundleSlugRenamer uses IStorageFactory.For(folderId, driverId,
        // path) — the test's FixedStorageFactory returns the SAME storage
        // instance regardless of folder. We exercise the multi-folder loop
        // via two folder records that share the test storage, each holding
        // its own blueprint file.
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext(folderPath: "/lib1");

        context.Folders.Add(
            entity: new()
            {
                Id = Ulid.NewUlid(),
                Path = "/lib2",
                DriverId = context.Drivers.First().Id,
            }
        );
        await context.SaveChangesAsync();

        const string path1 = "lib1/Item One/.nomercy.json";
        const string path2 = "lib2/Item Two/.nomercy.json";
        storage.Seed(
            path: path1,
            bytes: Encoding.UTF8.GetBytes(s: RenameTestHelpers.BuildBlueprintJson(presetSlugs: "old-slug"))
        );
        storage.Seed(
            path: path2,
            bytes: Encoding.UTF8.GetBytes(s: RenameTestHelpers.BuildBlueprintJson(presetSlugs: "old-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            slugMap: new() { [key: "old-slug"] = "new-slug" },
            storage: storage,
            context: context
        );

        await renamer.RunAsync();

        RenameTestHelpers
            .EncodeSlugs(blueprint: RenameTestHelpers.ReadBlueprint(storage: storage, path: path1))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(expected: "new-slug");
        RenameTestHelpers
            .EncodeSlugs(blueprint: RenameTestHelpers.ReadBlueprint(storage: storage, path: path2))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(expected: "new-slug");
    }
}
