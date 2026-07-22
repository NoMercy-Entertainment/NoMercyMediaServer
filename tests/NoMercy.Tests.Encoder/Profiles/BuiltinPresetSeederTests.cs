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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// BuiltinPresetSeeder upserts every built-in preset on startup and prunes
/// orphan built-in rows whose Ulids no longer match the shipped set. Without
/// this seeder, users would be stuck with whatever built-ins shipped in the
/// version they first installed.
/// </summary>
public class BuiltinPresetSeederTests
{
    private static MediaContext NewContext()
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase(databaseName: $"seeder-{Ulid.NewUlid()}")
            .Options;
        return new(options: options);
    }

    [Fact]
    public async Task SeedAsync_EmptyDb_InsertsEveryBuiltin()
    {
        // First boot: the encoder_presets table is empty. The seeder inserts
        // exactly the set returned by BuiltinPresets.All() — no more, no less.
        await using MediaContext ctx = NewContext();
        BuiltinPresetSeeder subject = new(context: ctx);

        await subject.SeedAsync();

        EncodingProfile[] expected = BuiltinPresets.All();
        List<EncodingPreset> seeded = await ctx.EncodingPresets.ToListAsync();
        seeded.Should().HaveCount(expected: expected.Length);
        seeded.Should().OnlyContain(predicate: p => p.IsBuiltIn);
        seeded.Should().OnlyContain(predicate: p => p.Source == "builtin");
        seeded.Select(selector: p => p.Id).Should().BeEquivalentTo(expectation: expected.Select(selector: p => p.Id));
    }

    [Fact]
    public async Task SeedAsync_ExistingMatchingBuiltin_UpdatesInPlace()
    {
        // A built-in row already exists with stale name/description (e.g.
        // the user installed v1.0 and we just shipped v1.1 with renamed copy).
        // The seeder must UPDATE in place, not insert a duplicate.
        await using MediaContext ctx = NewContext();
        EncodingProfile firstBuiltin = BuiltinPresets.All()[0];

        ctx.EncodingPresets.Add(
            entity: new()
            {
                Id = firstBuiltin.Id,
                Name = "Stale Name",
                Description = "Stale Description",
                ProfileJson = "{}",
                IsBuiltIn = true,
                Source = "builtin",
            }
        );
        await ctx.SaveChangesAsync();

        BuiltinPresetSeeder subject = new(context: ctx);
        await subject.SeedAsync();

        EncodingPreset? row = await ctx.EncodingPresets.FirstOrDefaultAsync(predicate: p =>
            p.Id == firstBuiltin.Id
        );
        row.Should().NotBeNull();
        row!.Name.Should().Be(expected: firstBuiltin.Name);
        row.Description.Should().Be(expected: firstBuiltin.Description);
        row.ProfileJson.Should().NotBe(unexpected: "{}");
    }

    [Fact]
    public async Task SeedAsync_StaleBuiltinRow_IsDropped()
    {
        // A stale built-in row (from a previous shipped version) remains in
        // the DB. Its Ulid is not in the current builtins set — seeder must
        // remove it so the dashboard list matches reality.
        await using MediaContext ctx = NewContext();
        Ulid staleId = Ulid.NewUlid();
        ctx.EncodingPresets.Add(
            entity: new()
            {
                Id = staleId,
                Name = "Removed Built-in",
                Description = "Was shipped in v0.9, no longer ships",
                ProfileJson = "{}",
                IsBuiltIn = true,
                Source = "builtin",
            }
        );
        await ctx.SaveChangesAsync();

        BuiltinPresetSeeder subject = new(context: ctx);
        await subject.SeedAsync();

        bool exists = await ctx.EncodingPresets.AnyAsync(predicate: p => p.Id == staleId);
        exists.Should().BeFalse(because: "stale built-in rows must be pruned on every seed");
    }

    /// <summary>
    /// A built-in id is a hash of its name, so renaming one retires the old id.
    /// EncodingPresetFolders cascades on the preset FK: deleting the retired row
    /// would take the folder's link with it and the folder silently stops
    /// encoding. The user picked that preset, so it becomes theirs instead.
    /// </summary>
    [Fact]
    public async Task SeedAsync_StaleBuiltinWithFolderLink_IsDemotedNotDeleted()
    {
        await using MediaContext ctx = NewContext();
        Ulid retiredId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();

        ctx.EncodingPresets.Add(
            entity: new()
            {
                Id = retiredId,
                Name = "Anime HEVC 1080p 10-bit",
                Description = "Shipped before the presets were renamed",
                ProfileJson = "{}",
                IsBuiltIn = true,
                Source = "builtin",
            }
        );
        ctx.EncodingPresetFolders.Add(
            entity: new()
            {
                PresetId = retiredId,
                FolderId = folderId,
                IsDefault = true,
            }
        );
        await ctx.SaveChangesAsync();

        BuiltinPresetSeeder subject = new(context: ctx);
        await subject.SeedAsync();

        EncodingPreset? retired = await ctx.EncodingPresets.FirstOrDefaultAsync(predicate: p =>
            p.Id == retiredId
        );
        retired.Should().NotBeNull(because: "a preset a folder still points at must never be deleted");
        retired!.IsBuiltIn.Should().BeFalse();
        retired.Source.Should().Be(expected: "retired-builtin");

        bool linkSurvived = await ctx.EncodingPresetFolders.AnyAsync(predicate: link =>
            link.PresetId == retiredId && link.FolderId == folderId
        );
        linkSurvived.Should().BeTrue(because: "the folder must keep encoding exactly as configured");
    }

    [Fact]
    public async Task SeedAsync_UserCustomPreset_IsLeftAlone()
    {
        // User-authored preset (IsBuiltIn=false) MUST survive every seed run.
        // Dropping these would lose user customization on every upgrade.
        await using MediaContext ctx = NewContext();
        Ulid userId = Ulid.NewUlid();
        ctx.EncodingPresets.Add(
            entity: new()
            {
                Id = userId,
                Name = "My Custom",
                Description = "user-made",
                ProfileJson = "{}",
                IsBuiltIn = false,
                Source = "user",
            }
        );
        await ctx.SaveChangesAsync();

        BuiltinPresetSeeder subject = new(context: ctx);
        await subject.SeedAsync();

        EncodingPreset? row = await ctx.EncodingPresets.FirstOrDefaultAsync(predicate: p => p.Id == userId);
        row.Should().NotBeNull();
        row!.Name.Should().Be(expected: "My Custom");
        row.IsBuiltIn.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_RunTwice_IsIdempotent()
    {
        // Re-running the seeder must not duplicate rows or change content.
        await using MediaContext ctx = NewContext();
        BuiltinPresetSeeder subject = new(context: ctx);

        await subject.SeedAsync();
        int countAfterFirst = await ctx.EncodingPresets.CountAsync();
        await subject.SeedAsync();
        int countAfterSecond = await ctx.EncodingPresets.CountAsync();

        countAfterSecond.Should().Be(expected: countAfterFirst);
    }

    [Fact]
    public async Task SeedAsync_StoresProfileJsonForLookup()
    {
        // The seeder must persist the SERIALIZED profile so DbPresetLookup
        // can deserialize and PresetResolver can resolve inheritance. Empty
        // JSON would break the entire preset chain.
        await using MediaContext ctx = NewContext();
        EncodingProfile firstBuiltin = BuiltinPresets.All()[0];
        BuiltinPresetSeeder subject = new(context: ctx);

        await subject.SeedAsync();

        EncodingPreset? row = await ctx.EncodingPresets.FirstOrDefaultAsync(predicate: p =>
            p.Id == firstBuiltin.Id
        );
        row.Should().NotBeNull();
        row!.ProfileJson.Should().NotBeNullOrWhiteSpace();
        row.ProfileJson.Should().Contain(expected: firstBuiltin.Name);
    }
}
