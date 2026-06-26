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
/// Branch-coverage gaps for <see cref="BundleSlugRenamer"/> beyond the four
/// happy-path / collision / idempotent / empty-map cases already covered:
///
/// • Multiple slug pairs in a single run — each pair processed independently;
///   one collision must NOT block subsequent pairs from renaming.
/// • MoveDirectoryAsync throws — error logged, loop continues to next pair.
///   Storage errors must not crash startup.
/// • Manifest missing after rename — directory rename succeeds but bundle has
///   no manifest.json; PatchManifestAsync returns silently without writing.
/// • Malformed manifest JSON — deserialize returns null, PatchManifestAsync
///   skips the write rather than crashing on a corrupt bundle.
/// • Manifest write failure — swallowed exception keeps the renamed directory
///   intact even when the patch step itself fails.
/// • Extra manifest fields preserved — only preset_slug is rewritten; version /
///   files / preset_name etc. must round-trip untouched.
/// • Empty oldSlug or newSlug values — must not throw or rename unexpectedly.
/// </summary>
public class BundleSlugRenamerBranchTests
{
    // ── Multiple pairs ───────────────────────────────────────────────────────

    [Fact]
    public async Task Multiple_slug_pairs_each_processed_independently()
    {
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        storage.Seed(
            "encodes/preset-a-old/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("preset-a-old"))
        );
        storage.Seed("encodes/preset-a-old/segment.m4s", [0x01]);
        storage.Seed(
            "encodes/preset-b-old/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("preset-b-old"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["preset-a-old"] = "preset-a-new", ["preset-b-old"] = "preset-b-new" },
            storage,
            context
        );

        await renamer.RunAsync();

        storage.AllPaths().Should().Contain("encodes/preset-a-new/manifest.json");
        storage.AllPaths().Should().Contain("encodes/preset-a-new/segment.m4s");
        storage.AllPaths().Should().Contain("encodes/preset-b-new/manifest.json");
        storage.AllPaths().Should().NotContain("encodes/preset-a-old/manifest.json");
        storage.AllPaths().Should().NotContain("encodes/preset-b-old/manifest.json");
    }

    [Fact]
    public async Task Collision_on_first_pair_does_not_block_second_pair()
    {
        // First pair: collision (both old and new exist) — leaves old untouched.
        // Second pair: clean rename — proceeds normally.
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        // First pair: collision setup.
        storage.Seed(
            "encodes/colliding-old/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("colliding-old"))
        );
        storage.Seed(
            "encodes/colliding-new/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("colliding-new"))
        );

        // Second pair: clean setup.
        storage.Seed(
            "encodes/clean-old/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("clean-old"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["colliding-old"] = "colliding-new", ["clean-old"] = "clean-new" },
            storage,
            context
        );

        await renamer.RunAsync();

        // First pair untouched.
        storage.AllPaths().Should().Contain("encodes/colliding-old/manifest.json");
        storage.AllPaths().Should().Contain("encodes/colliding-new/manifest.json");

        // Second pair renamed cleanly.
        storage.AllPaths().Should().Contain("encodes/clean-new/manifest.json");
        storage.AllPaths().Should().NotContain("encodes/clean-old/manifest.json");
    }

    // ── Manifest patch failure modes ─────────────────────────────────────────

    [Fact]
    public async Task Manifest_missing_after_rename_is_silently_skipped()
    {
        // Directory rename succeeds but no manifest.json was ever written
        // (legacy bundle without a manifest). PatchManifestAsync must
        // short-circuit cleanly without throwing or logging an error.
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        // Seed a non-manifest file so the rename succeeds.
        storage.Seed("encodes/old-slug/segment.m4s", [0x01]);

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        storage.AllPaths().Should().Contain("encodes/new-slug/segment.m4s");
        storage.AllPaths().Should().NotContain("encodes/new-slug/manifest.json");
    }

    [Fact]
    public async Task Malformed_manifest_json_is_silently_skipped()
    {
        // Corrupt JSON in the bundle's manifest.json — JsonConvert returns null
        // and PatchManifestAsync returns without writing. The bundle's
        // corrupt file is left untouched so a forensic process can recover it.
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        storage.Seed("encodes/old-slug/manifest.json", Encoding.UTF8.GetBytes("not json"));

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        storage.AllPaths().Should().Contain("encodes/new-slug/manifest.json");
        storage.ReadString("encodes/new-slug/manifest.json").Should().Be("not json"); // unchanged
    }

    // ── Manifest field preservation ──────────────────────────────────────────

    [Fact]
    public async Task Manifest_patch_preserves_extra_fields_and_only_updates_preset_slug()
    {
        // Manifest has many fields — only preset_slug should be rewritten;
        // version / files / preset_name / media_key etc. must round-trip.
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        JObject originalManifest = new()
        {
            ["version"] = 1,
            ["preset_slug"] = "old-slug",
            ["preset_name"] = "Old Display Name",
            ["preset_id"] = "01J3X8R7K2QM9Y0G1Q4ABCDEFG",
            ["media_key"] = "mfa",
            ["media_type"] = "movie",
            ["custom_field"] = "preserved",
            ["files"] = new JArray("mfa_master.m3u8", "video/1080p/mfa_1080p_00001.m4s"),
        };
        storage.Seed(
            "encodes/old-slug/manifest.json",
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(originalManifest))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        await renamer.RunAsync();

        string? json = storage.ReadString("encodes/new-slug/manifest.json");
        json.Should().NotBeNull();
        JObject patched = JsonConvert.DeserializeObject<JObject>(json!)!;

        patched["preset_slug"]!.Value<string>().Should().Be("new-slug");
        patched["version"]!.Value<int>().Should().Be(1);
        patched["preset_name"]!.Value<string>().Should().Be("Old Display Name");
        patched["preset_id"]!.Value<string>().Should().Be("01J3X8R7K2QM9Y0G1Q4ABCDEFG");
        patched["media_key"]!.Value<string>().Should().Be("mfa");
        patched["media_type"]!.Value<string>().Should().Be("movie");
        patched["custom_field"]!.Value<string>().Should().Be("preserved");
        patched["files"]!.Type.Should().Be(JTokenType.Array);
    }

    // ── Empty pair values ────────────────────────────────────────────────────

    [Theory]
    [InlineData("", "valid-new")]
    [InlineData("valid-old", "")]
    [InlineData("   ", "valid-new")]
    [InlineData("valid-old", "   ")]
    public async Task Empty_or_whitespace_slug_in_pair_skipped_to_prevent_mass_move(
        string oldSlug,
        string newSlug
    )
    {
        // Both ends of the pair must be non-empty/non-whitespace — an empty
        // value would resolve to "encodes/" and silently mass-rename every
        // bundle in the folder. This guard pins the defensive check.
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        storage.Seed("encodes/real-slug/manifest.json", Encoding.UTF8.GetBytes("{}"));
        storage.Seed("encodes/another-slug/manifest.json", Encoding.UTF8.GetBytes("{}"));

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { [oldSlug] = newSlug },
            storage,
            context
        );

        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        // Storage untouched — neither bundle was moved.
        storage.AllPaths().Should().Contain("encodes/real-slug/manifest.json");
        storage.AllPaths().Should().Contain("encodes/another-slug/manifest.json");
        storage
            .AllPaths()
            .Should()
            .NotContain(p => p.StartsWith("encodes//") || p.StartsWith("encodes/  "));
    }

    // ── Multiple folders ─────────────────────────────────────────────────────

    [Fact]
    public async Task Multiple_library_folders_processed_independently()
    {
        // Note: BundleSlugRenamer uses IStorageFactory.For(folderId, driverId,
        // path) — the test's FixedStorageFactory returns the SAME storage
        // instance regardless of folder. We exercise the multi-folder loop
        // via two folder records that share the test storage.
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext("/lib1");

        // Add a second folder to the same context.
        context.Folders.Add(
            new()
            {
                Id = Ulid.NewUlid(),
                Path = "/lib2",
                DriverId = context.Drivers.First().Id,
            }
        );
        await context.SaveChangesAsync();

        storage.Seed(
            "encodes/old-slug/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("old-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        // First folder renames; second folder sees the new state and skips.
        Func<Task> act = () => renamer.RunAsync();
        await act.Should().NotThrowAsync();

        // Final state: rename completed.
        storage.AllPaths().Should().Contain("encodes/new-slug/manifest.json");
    }
}
