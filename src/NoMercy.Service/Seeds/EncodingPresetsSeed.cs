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
using NoMercy.Encoder.Codecs;
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
/// ParentPresetId. Materialization of V2 EncodingPresets into V1 EncoderProfile
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
    /// Reverse migration from V2 EncodingPresets to V1 EncoderProfiles: the
    /// bridge the "Effortless Encoder" needs on a fresh install, since the V1
    /// <c>EncoderProfile</c> table is never seeded on its own (there are no
    /// production callers of <see cref="NoMercy.Data.Repositories.EncoderRepository.AddEncoderProfileAsync"/>).
    /// Without this, <c>BackfillV1ToV2Async</c> below has nothing to read and
    /// <c>context.EncoderProfiles.Any()</c> stays false forever, so the legacy
    /// auto-encode subscriber never dispatches.
    ///
    /// Projects every built-in V2 <see cref="EncodingPreset"/> (<c>IsBuiltIn</c>)
    /// into a matching V1 <see cref="EncoderProfile"/> row, reusing the preset's
    /// Ulid so the mapping is stable and idempotent — re-running only refreshes
    /// the same rows, never duplicates them. Only built-ins are materialized:
    /// user-authored presets that inherit via <c>ParentPresetId</c> with an
    /// empty <c>ProfileJson</c> would need full parent-chain resolution first,
    /// which is out of scope here.
    ///
    /// Known lossy edges (V1 has no vocabulary for them): auto-ladder rungs
    /// (<see cref="LadderMode.Auto"/>) aren't enumerable, so only the preset's
    /// base <c>Video</c> is materialized — the ladder itself stays V2-only;
    /// <see cref="SubtitleCodecType.Pgs"/> falls back to "copy" (there's no V1
    /// PGS token, and forcing WebVTT would silently transcode an image subtitle).
    /// </summary>
    public static async Task MaterializePresetsAsync(
        MediaContext context,
        CancellationToken ct = default
    )
    {
        List<EncodingPreset> builtinPresets = await context
            .EncodingPresets.AsNoTracking()
            .Where(p => p.IsBuiltIn)
            .ToListAsync(ct);

        if (builtinPresets.Count == 0)
            return;

        Dictionary<Ulid, EncoderProfile> existingByIdTracked =
            await context.EncoderProfiles.ToDictionaryAsync(p => p.Id, ct);

        int insertedProfiles = 0;
        int refreshedProfiles = 0;
        foreach (EncodingPreset preset in builtinPresets)
        {
            EncodingProfile v2Profile;
            try
            {
                v2Profile =
                    JsonConvert.DeserializeObject<EncodingProfile>(preset.ProfileJson)
                    ?? throw new InvalidOperationException("Deserialized to null.");
            }
            catch (Exception ex)
            {
                Logger.Setup(
                    $"MaterializePresets: skipping built-in preset '{preset.Name}' ({preset.Id}) — deserialize failed: {ex.Message}",
                    LogEventLevel.Warning
                );
                continue;
            }

            VideoProfile[] videoProfiles = BuildV1VideoProfiles(v2Profile);
            AudioProfile[] audioProfiles = v2Profile.Audio.Select(MapV2AudioToV1).ToArray();
            SubtitleProfile[] subtitleProfiles = v2Profile
                .Subtitles.Select(MapV2SubtitleToV1)
                .ToArray();
            ThumbnailProfile? thumbnails = v2Profile.Thumbnails is not null
                ? new()
                {
                    Width = v2Profile.Thumbnails.Width,
                    IntervalSeconds = v2Profile.Thumbnails.IntervalSeconds,
                }
                : null;
            string container = MapV2ContainerToV1(v2Profile.Container);

            if (existingByIdTracked.TryGetValue(preset.Id, out EncoderProfile? existing))
            {
                existing.Name = preset.Name;
                existing.Container = container;
                existing.VideoProfiles = videoProfiles;
                existing.AudioProfiles = audioProfiles;
                existing.SubtitleProfiles = subtitleProfiles;
                existing.Thumbnails = thumbnails;
                refreshedProfiles++;
            }
            else
            {
                context.EncoderProfiles.Add(
                    new()
                    {
                        Id = preset.Id,
                        Name = preset.Name,
                        Container = container,
                        VideoProfiles = videoProfiles,
                        AudioProfiles = audioProfiles,
                        SubtitleProfiles = subtitleProfiles,
                        Thumbnails = thumbnails,
                    }
                );
                insertedProfiles++;
            }
        }

        if (insertedProfiles > 0 || refreshedProfiles > 0)
        {
            await context.SaveChangesAsync(ct);
            Logger.Setup(
                $"MaterializePresets: inserted {insertedProfiles}, refreshed {refreshedProfiles} V1 EncoderProfile row(s) from built-in V2 presets",
                LogEventLevel.Information
            );
        }
    }

    private static VideoProfile[] BuildV1VideoProfiles(EncodingProfile profile)
    {
        if (profile.Video is null)
            return [];

        List<VideoProfile> profiles = [MapV2VideoToV1(profile.Video)];

        if (profile.Ladder is { Mode: LadderMode.Manual, Rungs.Length: > 0 })
        {
            profiles.AddRange(
                profile.Ladder.Rungs.Select(rung => MapV2LadderRungToV1(rung, profile.Video))
            );
        }

        return profiles.ToArray();
    }

    private static VideoProfile MapV2VideoToV1(VideoOutput v) =>
        new()
        {
            Codec = MapV2VideoCodecToV1(v.Codec),
            Bitrate = v.BitrateKbps,
            Width = v.Width,
            Height = v.Height ?? 0,
            Preset = v.Preset ?? string.Empty,
            Profile = MapV2CodecProfileToV1(v.CodecProfile),
            Tune = v.Tune ?? string.Empty,
            Level = v.Level ?? string.Empty,
            SegmentName = v.SegmentNameTemplate,
            PlaylistName = v.PlaylistNameTemplate,
            ColorSpace = v.PixelFormat ?? string.Empty,
            Crf = v.Crf,
            KeyInt = v.KeyframeIntervalSeconds,
            ConvertHdrToSdr = v.ConvertHdrToSdr,
            CustomArguments = v.CustomArguments?.Select(kv => (kv.Key, kv.Value)).ToArray() ?? [],
        };

    private static VideoProfile MapV2LadderRungToV1(LadderRung rung, VideoOutput baseVideo) =>
        new()
        {
            Codec = MapV2VideoCodecToV1(rung.Codec),
            Bitrate = rung.BitrateKbps,
            Width = rung.Width,
            Height = rung.Height,
            Preset = rung.Preset ?? baseVideo.Preset ?? string.Empty,
            Profile = MapV2CodecProfileToV1(rung.CodecProfile),
            Tune = baseVideo.Tune ?? string.Empty,
            Level = baseVideo.Level ?? string.Empty,
            SegmentName = baseVideo.SegmentNameTemplate,
            PlaylistName = baseVideo.PlaylistNameTemplate,
            ColorSpace = rung.PixelFormat ?? string.Empty,
            Crf = 0,
            KeyInt = baseVideo.KeyframeIntervalSeconds,
            ConvertHdrToSdr = baseVideo.ConvertHdrToSdr,
            CustomArguments = [],
        };

    private static AudioProfile MapV2AudioToV1(AudioOutput a) =>
        new()
        {
            Codec = MapV2AudioCodecToV1(a.Codec),
            Channels = a.Channels,
            SampleRate = a.SampleRateHz,
            SegmentName = a.SegmentNameTemplate,
            PlaylistName = a.PlaylistNameTemplate,
            AllowedLanguages = a.AllowedLanguages,
            CustomArguments = a.CustomArguments?.Select(kv => (kv.Key, kv.Value)).ToArray() ?? [],
            Loudness = MapV2LoudnessToV1(a.Loudness),
            Downmix = MapV2DownmixToV1(a.Downmix),
            CustomPanMatrix = a.Downmix?.CustomPanMatrix,
        };

    private static SubtitleProfile MapV2SubtitleToV1(SubtitleOutput s) =>
        new()
        {
            Codec = MapV2SubtitleCodecToV1(s.Codec),
            PlaylistName = s.PlaylistNameTemplate,
            AllowedLanguages = s.AllowedLanguages,
            CustomArguments = s.CustomArguments?.Select(kv => (kv.Key, kv.Value)).ToArray() ?? [],
        };

    private static string MapV2ContainerToV1(Container container) =>
        container switch
        {
            Container.HlsTs => "hls_ts",
            Container.HlsFmp4 => "m3u8",
            Container.AudioHlsTs => "hls_ts",
            Container.AudioHlsFmp4 => "m3u8",
            Container.Mkv => "mkv",
            Container.Mp4 => "mp4",
            Container.Dash => "dash",
            Container.Mp3 => "mp3",
            Container.Aac => "aac",
            Container.Flac => "flac",
            Container.Ogg => "ogg",
            Container.Mka => "mka",
            Container.Mks => "mks",
            _ => "m3u8",
        };

    private static string MapV2VideoCodecToV1(VideoCodecType codec) =>
        codec switch
        {
            VideoCodecType.H264 => "h264",
            VideoCodecType.H265 => "hevc",
            VideoCodecType.Av1 => "av1",
            VideoCodecType.Vp9 => "vp9",
            VideoCodecType.Copy => "copy",
            _ => "h264",
        };

    private static string MapV2AudioCodecToV1(AudioCodecType codec) =>
        codec switch
        {
            AudioCodecType.Aac => "aac",
            AudioCodecType.Flac => "flac",
            AudioCodecType.Opus => "opus",
            AudioCodecType.Ac3 => "ac3",
            AudioCodecType.Eac3 => "eac3",
            AudioCodecType.Mp3 => "mp3",
            AudioCodecType.Vorbis => "vorbis",
            AudioCodecType.TrueHd => "truehd",
            AudioCodecType.Dts => "dts",
            AudioCodecType.Copy => "copy",
            _ => "aac",
        };

    // No V1 token exists for PGS image subtitles — falling back to WebVtt would
    // silently transcode them, so this maps to "copy" (stream copy) instead,
    // matching the safer of the two lossy options.
    private static string MapV2SubtitleCodecToV1(SubtitleCodecType codec) =>
        codec switch
        {
            SubtitleCodecType.WebVtt => "webvtt",
            SubtitleCodecType.Srt => "srt",
            SubtitleCodecType.Ass => "ass",
            SubtitleCodecType.Copy => "copy",
            SubtitleCodecType.Pgs => "copy",
            _ => "webvtt",
        };

    private static string MapV2CodecProfileToV1(CodecProfile profile) =>
        profile switch
        {
            CodecProfile.Baseline => "baseline",
            CodecProfile.Main => "main",
            CodecProfile.Main10 => "main10",
            CodecProfile.High => "high",
            CodecProfile.High10 => "high10",
            _ => string.Empty,
        };

    private static string? MapV2LoudnessToV1(LoudnessConfig? loudness) =>
        loudness?.Mode switch
        {
            LoudnessMode.EbuR128 => "ebu_r128",
            LoudnessMode.ReplayGain => "replaygain",
            LoudnessMode.Custom => "custom",
            _ => null,
        };

    private static string? MapV2DownmixToV1(DownmixConfig? downmix) =>
        downmix?.Mode switch
        {
            DownmixMode.StereoItuR128 => "stereo",
            DownmixMode.Mono => "mono",
            DownmixMode.Custom => "custom",
            _ => null,
        };

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

    private static V1VideoProfile MapVideo(VideoProfile v) =>
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

    private static V1AudioProfile MapAudio(AudioProfile a) =>
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

    private static V1SubtitleProfile MapSubtitle(SubtitleProfile s) =>
        new(
            Codec: s.Codec,
            PlaylistName: s.PlaylistName,
            AllowedLanguages: s.AllowedLanguages,
            CustomArguments: s.CustomArguments
        );
}
