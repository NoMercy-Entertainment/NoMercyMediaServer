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
    /// For every <see cref="EncodingPreset"/> row that carries a parseable
    /// <c>ProfileJson</c>, materializes a corresponding <see cref="EncoderProfile"/>
    /// row so V3 presets appear in the Folder picker alongside V1 profiles.
    ///
    /// Rules:
    ///   - Uses the preset's Id as the EncoderProfile Id (stable, no duplicates).
    ///   - Never touches EncoderProfileFolder rows — folder assignments survive re-seed.
    ///   - Unparseable ProfileJson is skipped with a warning; one bad preset must
    ///     not block the rest.
    /// </summary>
    public static Task MaterializePresetsAsync(MediaContext context)
    {
        // V2 → V1 bridge retired with the V2 schema migration. ProfileMapper
        // (which converted V2 EncodingProfile → V1 EncoderProfile column shape)
        // was deleted with the V2.5 cleanup, and the V1 EncoderProfile DB model
        // is itself slated for removal. This method now no-ops; the dashboard
        // queries EncodingPresets directly via /api/v1/encoder/profiles
        // endpoints, and the legacy folder picker will only see V1 rows that
        // were seeded explicitly via the file-based seeder (which is also gone)
        // or persisted from a prior release.
        Logger.Setup(
            "MaterializePresetsAsync: skipped — V2→V1 bridge retired. "
                + "EncodingPresets is the authoritative source.",
            LogEventLevel.Verbose
        );
        return Task.CompletedTask;
    }
}
