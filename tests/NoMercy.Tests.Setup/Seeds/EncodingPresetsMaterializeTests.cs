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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Profiles;
using NoMercy.Service.Seeds;
using V2BuiltinPresets = NoMercy.Encoder.Profiles.BuiltinPresets;

namespace NoMercy.Tests.Setup.Seeds;

/// <summary>
/// Covers the missing V2 -> V1 bridge: on a fresh install the legacy
/// EncoderProfile table is never seeded on its own, so
/// <see cref="EncodingPresetsSeed.MaterializePresetsAsync"/> is the only
/// path that makes <c>context.EncoderProfiles.Any()</c> become true.
/// </summary>
[Trait("Category", "Unit")]
public class EncodingPresetsMaterializeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MediaContext> _options;

    public EncodingPresetsMaterializeTests()
    {
        _connection = new("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaContext>()
            .UseSqlite(
                _connection,
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            )
            .Options;

        using MediaContext ctx = new(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private MediaContext CreateContext() => new(_options);

    private async Task SeedBuiltinsAsync()
    {
        await using MediaContext ctx = CreateContext();
        await new BuiltinPresetSeeder(ctx).SeedAsync();
    }

    [Fact]
    public async Task FreshInstall_MaterializesEveryBuiltinIntoV1EncoderProfiles()
    {
        await SeedBuiltinsAsync();

        await using (MediaContext ctx = CreateContext())
        {
            await EncodingPresetsSeed.MaterializePresetsAsync(ctx);
        }

        await using MediaContext verify = CreateContext();

        int builtinCount = V2BuiltinPresets.All().Length;
        int profileCount = await verify.EncoderProfiles.CountAsync();

        Assert.Equal(builtinCount, profileCount);
        Assert.True(await verify.EncoderProfiles.AnyAsync());
    }

    [Fact]
    public async Task MaterializedProfiles_ReuseThePresetUlid()
    {
        await SeedBuiltinsAsync();

        await using (MediaContext ctx = CreateContext())
        {
            await EncodingPresetsSeed.MaterializePresetsAsync(ctx);
        }

        await using MediaContext verify = CreateContext();

        List<Ulid> presetIds = await verify
            .EncodingPresets.Where(p => p.IsBuiltIn)
            .Select(p => p.Id)
            .ToListAsync();
        List<Ulid> profileIds = await verify.EncoderProfiles.Select(p => p.Id).ToListAsync();

        Assert.Equal(presetIds.OrderBy(id => id), profileIds.OrderBy(id => id));
    }

    [Fact]
    public async Task SecondRun_IsIdempotent_NoDuplicateRows()
    {
        await SeedBuiltinsAsync();

        await using (MediaContext firstRun = CreateContext())
        {
            await EncodingPresetsSeed.MaterializePresetsAsync(firstRun);
        }

        await using (MediaContext secondRun = CreateContext())
        {
            await EncodingPresetsSeed.MaterializePresetsAsync(secondRun);
        }

        await using MediaContext verify = CreateContext();

        int builtinCount = V2BuiltinPresets.All().Length;
        int profileCount = await verify.EncoderProfiles.CountAsync();
        Assert.Equal(builtinCount, profileCount);
    }

    [Fact]
    public async Task NonBuiltinPreset_IsNeverMaterialized()
    {
        Ulid userPresetId = Ulid.NewUlid();

        await using (MediaContext seed = CreateContext())
        {
            seed.EncodingPresets.Add(
                new()
                {
                    Id = userPresetId,
                    Name = "User Custom Preset",
                    ProfileJson = "{}",
                    IsBuiltIn = false,
                    Source = "seed",
                }
            );
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await EncodingPresetsSeed.MaterializePresetsAsync(ctx);
        }

        await using MediaContext verify = CreateContext();

        EncoderProfile? profile = await verify.EncoderProfiles.FindAsync(userPresetId);
        Assert.Null(profile);
        Assert.Equal(0, await verify.EncoderProfiles.CountAsync());
    }

    [Fact]
    public async Task EmptyEncodingPresets_DoesNotThrow_AndLeavesTableEmpty()
    {
        await using MediaContext ctx = CreateContext();
        await EncodingPresetsSeed.MaterializePresetsAsync(ctx);

        Assert.Equal(0, await ctx.EncoderProfiles.CountAsync());
    }

    [Fact]
    public async Task Web1080pBalanced_MapsVideoAudioAndSubtitleFaithfully()
    {
        await SeedBuiltinsAsync();

        await using (MediaContext ctx = CreateContext())
        {
            await EncodingPresetsSeed.MaterializePresetsAsync(ctx);
        }

        EncodingProfile source = V2BuiltinPresets.All().First(p => p.Name == "Web 1080p Balanced");

        await using MediaContext verify = CreateContext();
        EncoderProfile? profile = await verify.EncoderProfiles.FindAsync(source.Id);

        Assert.NotNull(profile);
        Assert.Equal("hls_ts", profile.Container);

        Assert.Single(profile.VideoProfiles);
        VideoProfile video = profile.VideoProfiles[0];
        Assert.Equal("h264", video.Codec);
        Assert.Equal(1920, video.Width);
        Assert.Equal(1080, video.Height);
        Assert.Equal(22, video.Crf);
        Assert.Equal("high", video.Profile);

        Assert.Single(profile.AudioProfiles);
        AudioProfile audio = profile.AudioProfiles[0];
        Assert.Equal("aac", audio.Codec);
        Assert.Equal(2, audio.Channels);
        Assert.Equal(48000, audio.SampleRate);

        Assert.Single(profile.SubtitleProfiles);
        Assert.Equal("webvtt", profile.SubtitleProfiles[0].Codec);
    }

    [Fact]
    public async Task AudioOnlyBuiltin_HasEmptyVideoProfiles()
    {
        await SeedBuiltinsAsync();

        await using (MediaContext ctx = CreateContext())
        {
            await EncodingPresetsSeed.MaterializePresetsAsync(ctx);
        }

        EncodingProfile source = V2BuiltinPresets.All().First(p => p.Name == "Music FLAC Lossless");

        await using MediaContext verify = CreateContext();
        EncoderProfile? profile = await verify.EncoderProfiles.FindAsync(source.Id);

        Assert.NotNull(profile);
        Assert.Equal("flac", profile.Container);
        Assert.Empty(profile.VideoProfiles);
        Assert.Single(profile.AudioProfiles);
        Assert.Equal("flac", profile.AudioProfiles[0].Codec);
    }

    [Fact]
    public async Task StaleUserEditedV1Row_IsRefreshedOnRerun_NotDuplicated()
    {
        await SeedBuiltinsAsync();

        EncodingProfile source = V2BuiltinPresets.All().First(p => p.Name == "Web 720p Fast");

        // Simulate a stale V1 row (e.g. left over from a previous version)
        // sharing the same Ulid with a different name.
        await using (MediaContext seed = CreateContext())
        {
            seed.EncoderProfiles.Add(
                new()
                {
                    Id = source.Id,
                    Name = "Stale Name",
                    Container = "mp4",
                }
            );
            await seed.SaveChangesAsync();
        }

        await using (MediaContext ctx = CreateContext())
        {
            await EncodingPresetsSeed.MaterializePresetsAsync(ctx);
        }

        await using MediaContext verify = CreateContext();
        EncoderProfile? profile = await verify.EncoderProfiles.FindAsync(source.Id);

        Assert.NotNull(profile);
        Assert.Equal(source.Name, profile.Name);
        Assert.Equal("hls_ts", profile.Container);
        Assert.Equal(1, await verify.EncoderProfiles.CountAsync(p => p.Id == source.Id));
    }
}
