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
            path,
            Encoding.UTF8.GetBytes(
                RenameTestHelpers.BuildBlueprintJson(["preset-a-old", "preset-b-old", "untouched"])
            )
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["preset-a-old"] = "preset-a-new", ["preset-b-old"] = "preset-b-new" },
            storage,
            context
        );

        await renamer.RunAsync();

        IReadOnlyList<string> slugs = RenameTestHelpers.EncodeSlugs(
            RenameTestHelpers.ReadBlueprint(storage, path)
        );
        slugs.Should().BeEquivalentTo(["preset-a-new", "preset-b-new", "untouched"]);
    }

    [Fact]
    public async Task Malformed_blueprint_json_is_skipped_and_does_not_block_other_items()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string corruptPath = "Corrupt Item/.nomercy.json";
        const string validPath = "Good Item/.nomercy.json";

        storage.Seed(corruptPath, Encoding.UTF8.GetBytes("not valid json"));
        storage.Seed(
            validPath,
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildBlueprintJson("old-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        // Corrupt file is left untouched for forensic recovery.
        storage.ReadString(corruptPath).Should().Be("not valid json");

        RenameTestHelpers
            .EncodeSlugs(RenameTestHelpers.ReadBlueprint(storage, validPath))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("new-slug");
    }

    [Fact]
    public async Task Blueprint_without_encodes_array_is_skipped_without_throwing()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Legacy Item/.nomercy.json";
        string legacyJson = JsonConvert.SerializeObject(
            new JObject
            {
                ["version"] = 1,
                ["identity"] = new JObject { ["type"] = "movie" },
            }
        );
        storage.Seed(path, Encoding.UTF8.GetBytes(legacyJson));

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        storage.ReadString(path).Should().Be(legacyJson);
    }

    [Fact]
    public async Task Extra_blueprint_fields_and_unmatched_encode_fields_round_trip_untouched()
    {
        TestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        const string path = "Fight Club (1999)/.nomercy.json";
        JObject original = new()
        {
            ["version"] = 1,
            ["identity"] = new JObject
            {
                ["type"] = "movie",
                ["tmdb_id"] = 550,
                ["title"] = "Fight Club",
            },
            ["source"] = new JObject { ["path"] = "Fight Club.mkv" },
            ["encodes"] = new JArray(
                new JObject
                {
                    ["preset_slug"] = "old-slug",
                    ["preset_id"] = "01J3X8R7K2QM9Y0G1Q4ABCDEFG",
                    ["profile_fingerprint"] = "abc123",
                    ["custom_field"] = "preserved",
                }
            ),
        };
        storage.Seed(path, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(original)));

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        await renamer.RunAsync();

        JObject patched = RenameTestHelpers.ReadBlueprint(storage, path);
        JObject encode = (JObject)patched["encodes"]![0]!;

        encode["preset_slug"]!.Value<string>().Should().Be("new-slug");
        encode["preset_id"]!.Value<string>().Should().Be("01J3X8R7K2QM9Y0G1Q4ABCDEFG");
        encode["profile_fingerprint"]!.Value<string>().Should().Be("abc123");
        encode["custom_field"]!.Value<string>().Should().Be("preserved");
        patched["version"]!.Value<int>().Should().Be(1);
        patched["identity"]!["title"]!.Value<string>().Should().Be("Fight Club");
        patched["source"]!["path"]!.Value<string>().Should().Be("Fight Club.mkv");
    }

    [Theory]
    [InlineData(["", "valid-new"])]
    [InlineData(["valid-old", ""])]
    [InlineData(["   ", "valid-new"])]
    [InlineData(["valid-old", "   "])]
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
            path,
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildBlueprintJson("real-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { [oldSlug] = newSlug },
            storage,
            context
        );

        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        RenameTestHelpers
            .EncodeSlugs(RenameTestHelpers.ReadBlueprint(storage, path))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("real-slug");
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
        MediaContext context = RenameTestHelpers.BuildInMemoryContext("/lib1");

        context.Folders.Add(
            new()
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
            path1,
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildBlueprintJson("old-slug"))
        );
        storage.Seed(
            path2,
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildBlueprintJson("old-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        await renamer.RunAsync();

        RenameTestHelpers
            .EncodeSlugs(RenameTestHelpers.ReadBlueprint(storage, path1))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("new-slug");
        RenameTestHelpers
            .EncodeSlugs(RenameTestHelpers.ReadBlueprint(storage, path2))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("new-slug");
    }
}
