using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.NewtonSoftConverters;
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

        NoMercy.Encoder.Profiles.EncodingProfile[] builtins = V2BuiltinPresets.All();
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
    /// For every built-in <see cref="EncodingPreset"/>, upserts a matching
    /// <see cref="EncoderProfile"/> row with the same Id so the V1-keyed
    /// folder picker and encode pipeline (<c>EncoderProfileFolder</c> FK still
    /// references EncoderProfiles, VideoEncodeJob still calls
    /// <c>V2ProfileFactory.FromV1</c>) see the V2 builtins.
    ///
    /// Refreshes columns on every boot so stale rows from earlier mapper
    /// versions get corrected. Never deletes existing V1 rows — folder
    /// assignments and any hand-edited user profiles stay put.
    /// </summary>
    public static async Task MaterializePresetsAsync(
        MediaContext context,
        CancellationToken ct = default
    )
    {
        List<EncodingPreset> presets = await context
            .EncodingPresets.AsNoTracking()
            .Where(p => p.IsBuiltIn)
            .ToListAsync(ct);

        int upserted = 0;
        int skipped = 0;

        foreach (EncodingPreset preset in presets)
        {
            NoMercy.Encoder.Profiles.EncodingProfile? v2Profile;
            try
            {
                v2Profile = JsonConvert.DeserializeObject<NoMercy.Encoder.Profiles.EncodingProfile>(
                    preset.ProfileJson
                );
            }
            catch (Exception ex)
            {
                Logger.Setup(
                    $"MaterializePresets: skipping '{preset.Name}' — failed to parse ProfileJson: {ex.Message}",
                    LogEventLevel.Warning
                );
                skipped++;
                continue;
            }

            if (v2Profile is null)
            {
                skipped++;
                continue;
            }

            V1ProfileFactory.V1Shape shape = V1ProfileFactory.FromV2(v2Profile);

            EncoderProfile? existing = await context.EncoderProfiles.FirstOrDefaultAsync(
                p => p.Id == preset.Id,
                ct
            );

            if (existing is null)
            {
                EncoderProfile created = new() { Id = preset.Id, Name = preset.Name };
                created.Container = shape.Container;
                created.VideoProfiles = shape.VideoProfiles;
                created.AudioProfiles = shape.AudioProfiles;
                created.SubtitleProfiles = shape.SubtitleProfiles;
                context.EncoderProfiles.Add(created);
            }
            else
            {
                existing.Name = preset.Name;
                existing.Container = shape.Container;
                existing.VideoProfiles = shape.VideoProfiles;
                existing.AudioProfiles = shape.AudioProfiles;
                existing.SubtitleProfiles = shape.SubtitleProfiles;
            }
            upserted++;
        }

        await context.SaveChangesAsync(ct);
        Logger.Setup(
            $"MaterializePresets: upserted {upserted}, skipped {skipped} V1 rows from {presets.Count} V2 builtins",
            LogEventLevel.Verbose
        );
    }
}
