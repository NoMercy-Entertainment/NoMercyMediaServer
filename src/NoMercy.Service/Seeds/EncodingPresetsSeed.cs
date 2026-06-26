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
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Service.Seeds.Data;
using NoMercy.Storage;
using Serilog.Events;
using V2BuiltinPresets = NoMercy.Encoder.Profiles.BuiltinPresets;

namespace NoMercy.Service.Seeds;

/// <summary>
/// Seeds the V2 built-in preset library and (when --seed is passed) a small
/// set of user-editable example presets that inherit from built-ins via
/// ParentPresetId. Materialization of V3 EncodingPresets into V1 EncoderProfile
/// rows happens immediately after in DatabaseSeeder.SeedOfflineData via
/// MaterializePresetsAsync.
/// </summary>
public static class EncodingPresetsSeed
{
    public static async Task Init(MediaContext context, IStorage storage)
    {
        Logger.Setup("Adding Encoding Presets", LogEventLevel.Verbose);

        try
        {
            // Builtins always seed on every startup, regardless of --seed flag.
            // BuiltinPresetSeeder handles stale-builtin pruning internally.
            await new BuiltinPresetSeeder(context).SeedAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }

    /// <summary>
    /// Seeds three user-editable example presets that inherit from built-ins
    /// via ParentPresetId with an empty ProfileJson — pure inheritance until
    /// the user edits them. Called only when --seed is passed.
    /// </summary>
    public static async Task SeedExamplesAsync(MediaContext context, CancellationToken ct = default)
    {
        Logger.Setup("Seeding example encoding presets", LogEventLevel.Verbose);

        EncodingProfile[] builtins = V2BuiltinPresets.All();
        Dictionary<string, Ulid> builtinByName = builtins.ToDictionary(p => p.Name, p => p.Id);

        foreach (EncoderProfileSeedData.SeedExample example in EncoderProfileSeedData.Examples)
        {
            if (!builtinByName.TryGetValue(example.ParentBuiltinName, out Ulid parentId))
            {
                Logger.Setup(
                    $"SeedExamples: built-in '{example.ParentBuiltinName}' not found — skipping '{example.Name}'",
                    LogEventLevel.Warning
                );
                continue;
            }

            bool exists = await context.EncodingPresets.AnyAsync(p => p.Name == example.Name, ct);

            if (exists)
                continue;

            context.EncodingPresets.Add(
                new()
                {
                    Id = Ulid.NewUlid(),
                    Name = example.Name,
                    Description =
                        $"User-editable starter example. Inherits from built-in '{example.ParentBuiltinName}'. Edit any field to override.",
                    ProfileJson = "{}",
                    ParentPresetId = parentId,
                    IsBuiltIn = false,
                    Source = "seed",
                }
            );
        }

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Forward migration from V1 EncoderProfiles to V2 EncodingPresets:
    ///   - For every legacy V1 <c>EncoderProfile</c> row that has no matching
    ///     V2 <c>EncodingPreset</c> by Ulid, build a V2 EncodingProfile via
    ///     <see cref="V2ProfileFactory.FromV1"/>, serialize it, and insert
    ///     an EncodingPreset row preserving the same Ulid.
    ///   - For every legacy <c>EncoderProfileFolder</c> link, ensure a
    ///     matching <c>EncodingPresetFolder</c> row exists. Same FolderId,
    ///     PresetId = EncoderProfileId.
    ///
    /// Idempotent — runs on every boot until the V1 tables get dropped in
    /// a follow-up migration. Once the V1 schema is gone this seed can be
    /// removed entirely.
    /// </summary>
    public static async Task BackfillV1ToV2Async(
        MediaContext context,
        CancellationToken ct = default
    )
    {
        // ── Step 1: copy V1 EncoderProfiles → V2 EncodingPresets ──────────────
        // Refreshes existing v1_backfill rows on every boot so default-value
        // changes (HardwarePreference, HdrPolicy, etc.) propagate to migrated
        // user profiles. Hand-edited V2 rows (Source != 'v1_backfill') are
        // never touched.
        Dictionary<Ulid, EncodingPreset> existingByIdTracked =
            await context.EncodingPresets.ToDictionaryAsync(p => p.Id, ct);

        List<EncoderProfile> v1Profiles = await context
            .EncoderProfiles.AsNoTracking()
            .ToListAsync(ct);

        int insertedPresets = 0;
        int refreshedPresets = 0;
        foreach (EncoderProfile v1 in v1Profiles)
        {
            EncodingProfile v2Profile;
            try
            {
                v2Profile = V2ProfileFactory.FromV1(
                    v1.Id,
                    v1.Name,
                    v1.Container ?? "m3u8",
                    v1.VideoProfiles.Select(MapVideo).ToArray(),
                    v1.AudioProfiles.Select(MapAudio).ToArray(),
                    v1.SubtitleProfiles.Select(MapSubtitle).ToArray(),
                    v1.Thumbnails is not null
                        ? new V1ThumbnailProfile(v1.Thumbnails.Width, v1.Thumbnails.IntervalSeconds)
                        : null
                );
            }
            catch (Exception ex)
            {
                Logger.Setup(
                    $"BackfillV1ToV2: skipping V1 profile '{v1.Name}' ({v1.Id}) — conversion failed: {ex.Message}",
                    LogEventLevel.Warning
                );
                continue;
            }

            string profileJson = JsonConvert.SerializeObject(v2Profile);

            if (existingByIdTracked.TryGetValue(v1.Id, out EncodingPreset? existing))
            {
                // Only refresh rows that came from a previous backfill pass —
                // hand-edited user presets that happen to share a V1 Ulid are
                // out of scope.
                if (existing.Source != "v1_backfill")
                    continue;

                if (existing.ProfileJson == profileJson && existing.Name == v1.Name)
                    continue;

                existing.Name = v1.Name;
                existing.ProfileJson = profileJson;
                refreshedPresets++;
            }
            else
            {
                context.EncodingPresets.Add(
                    new()
                    {
                        Id = v1.Id,
                        Name = v1.Name,
                        Description = $"Migrated from V1 EncoderProfile '{v1.Name}'.",
                        ProfileJson = profileJson,
                        IsBuiltIn = false,
                        Source = "v1_backfill",
                    }
                );
                insertedPresets++;
            }
        }

        if (insertedPresets > 0 || refreshedPresets > 0)
            await context.SaveChangesAsync(ct);

        // ── Step 2: copy V1 EncoderProfileFolder → V2 EncodingPresetFolders ───
        HashSet<(Ulid PresetId, Ulid FolderId)> existingLinks =
        [
            .. await context
                .EncodingPresetFolders.AsNoTracking()
                .Select(l => new ValueTuple<Ulid, Ulid>(l.PresetId, l.FolderId))
                .ToListAsync(ct),
        ];

        List<EncoderProfileFolder> v1Links = await context
            .Set<EncoderProfileFolder>()
            .AsNoTracking()
            .ToListAsync(ct);

        int copiedLinks = 0;
        foreach (EncoderProfileFolder v1Link in v1Links)
        {
            (Ulid PresetId, Ulid FolderId) key = (v1Link.EncoderProfileId, v1Link.FolderId);
            if (existingLinks.Contains(key))
                continue;

            // Only port the link when the target preset exists in V2 (either a
            // builtin or just-backfilled). Otherwise the FK would be invalid.
            bool presetExists = await context.EncodingPresets.AnyAsync(
                p => p.Id == v1Link.EncoderProfileId,
                ct
            );
            if (!presetExists)
            {
                Logger.Setup(
                    $"BackfillV1ToV2: dropping orphan link (V1 profile {v1Link.EncoderProfileId} → folder {v1Link.FolderId}) — no V2 preset target",
                    LogEventLevel.Warning
                );
                continue;
            }

            context.EncodingPresetFolders.Add(
                new()
                {
                    PresetId = v1Link.EncoderProfileId,
                    FolderId = v1Link.FolderId,
                    IsDefault = false,
                }
            );
            copiedLinks++;
        }

        if (copiedLinks > 0)
            await context.SaveChangesAsync(ct);

        if (insertedPresets > 0 || refreshedPresets > 0 || copiedLinks > 0)
        {
            Logger.Setup(
                $"BackfillV1ToV2: inserted {insertedPresets}, refreshed {refreshedPresets} preset(s) + ported {copiedLinks} link(s) from V1 to V2",
                LogEventLevel.Information
            );
        }
    }

    private static V1VideoProfile MapVideo(IVideoProfile v) =>
        new(
            Codec: v.Codec,
            Bitrate: v.Bitrate,
            Width: v.Width,
            Height: v.Height,
            Preset: v.Preset,
            Profile: v.Profile,
            Tune: v.Tune,
            Level: v.Level,
            SegmentName: v.SegmentName,
            PlaylistName: v.PlaylistName,
            ColorSpace: v.ColorSpace,
            Crf: v.Crf,
            KeyInt: v.KeyInt,
            ConvertHdrToSdr: v.ConvertHdrToSdr,
            CustomArguments: v.CustomArguments
        );

    private static V1AudioProfile MapAudio(IAudioProfile a) =>
        new(
            Codec: a.Codec,
            Channels: a.Channels,
            SampleRate: a.SampleRate,
            SegmentName: a.SegmentName,
            PlaylistName: a.PlaylistName,
            AllowedLanguages: a.AllowedLanguages,
            CustomArguments: a.CustomArguments,
            Loudness: a.Loudness,
            Downmix: a.Downmix,
            CustomPanMatrix: a.CustomPanMatrix
        );

    private static V1SubtitleProfile MapSubtitle(ISubtitleProfile s) =>
        new(
            Codec: s.Codec,
            PlaylistName: s.PlaylistName,
            AllowedLanguages: s.AllowedLanguages,
            CustomArguments: s.CustomArguments
        );
}
