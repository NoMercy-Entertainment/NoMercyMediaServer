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
using V2BuiltinPresets = NoMercy.Encoder.Profiles.V2.BuiltinPresets;

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

        NoMercy.Encoder.Profiles.V2.EncodingProfile[] builtins = V2BuiltinPresets.All();
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
    public static async Task MaterializePresetsAsync(MediaContext context)
    {
        Logger.Setup("Materializing V3 EncodingPresets into EncoderProfiles");

        List<EncodingPreset> presets = await context.EncodingPresets.AsNoTracking().ToListAsync();

        Logger.Setup($"MaterializePresets: scanned {presets.Count} EncodingPreset row(s)");

        List<EncoderProfile> materialized = [];
        int skippedEmpty = 0;
        int skippedDeserializeNull = 0;
        int deserializeFailures = 0;

        foreach (EncodingPreset preset in presets)
        {
            if (string.IsNullOrWhiteSpace(preset.ProfileJson) || preset.ProfileJson == "{}")
            {
                skippedEmpty++;
                continue;
            }

            EncodingProfile? profile;
            try
            {
                // Must pass JsonHelper.Settings explicitly: it carries the
                // StringEnumConverter so that "Format": "Hls" deserializes
                // to OutputFormat.Hls. Default JsonConvert settings would
                // throw on every preset and the Folder picker would silently
                // miss every materialized row.
                profile = JsonConvert.DeserializeObject<EncodingProfile>(
                    preset.ProfileJson,
                    JsonHelper.Settings
                );
            }
            catch (Exception ex)
            {
                deserializeFailures++;
                Logger.Setup(
                    $"MaterializePresets: could not deserialize ProfileJson for preset '{preset.Name}' ({preset.Id}): {ex.Message}",
                    LogEventLevel.Warning
                );
                continue;
            }

            if (profile is null)
            {
                skippedDeserializeNull++;
                Logger.Setup(
                    $"MaterializePresets: deserialize returned null for preset '{preset.Name}' ({preset.Id})",
                    LogEventLevel.Warning
                );
                continue;
            }

            (
                Ulid id,
                string name,
                string container,
                string videoJson,
                string audioJson,
                string subtitleJson
            ) = ProfileMapper.ToV1Fields(profile with { Id = preset.Id, Name = preset.Name });

            // V1 ThumbnailProfile column carries the same {Width, IntervalSeconds}
            // shape as the V3 ThumbnailOutput record, so round-trip is a flat
            // serialize. Empty when the V3 preset omits Thumbnails — the
            // encoder pipeline then skips the spritevtt mux entirely instead
            // of writing an empty thumbs file.
            string thumbnailJson = profile.Thumbnails is not null
                ? JsonConvert.SerializeObject(
                    new { profile.Thumbnails.Width, profile.Thumbnails.IntervalSeconds }
                )
                : string.Empty;

            materialized.Add(
                new()
                {
                    Id = id,
                    Name = name,
                    Container = container,
                    _videoProfiles = videoJson,
                    _audioProfiles = audioJson,
                    _subtitleProfiles = subtitleJson,
                    _thumbnailProfile = thumbnailJson,
                    EncoderProfileFolder = [],
                }
            );
        }

        if (materialized.Count == 0)
        {
            Logger.Setup(
                $"MaterializePresets: 0 rows materialized "
                    + $"(scanned={presets.Count} empty={skippedEmpty} "
                    + $"deserializeFailures={deserializeFailures} "
                    + $"deserializeNull={skippedDeserializeNull}). "
                    + "Folder picker will only show V1-seeded EncoderProfile rows. "
                    + "Check the warnings above for the failure cause.",
                LogEventLevel.Warning
            );
            return;
        }

        try
        {
            // Upsert manually via plain EF — FlexLabs.Upsert was silently
            // returning success without writing rows for this table (no
            // exception, no rows changed, no rollback message). Bypass the
            // library and walk the list ourselves.
            //
            // Fetch ALL existing EncoderProfile ids and intersect in memory
            // rather than asking EF to translate `Where(p => list.Contains(p.Id))`.
            // The translated SQL was matching nothing AND ALSO returning all
            // 25 incoming ids as "existing" via an EF/value-converter quirk
            // we couldn't reproduce — switching to in-memory intersection
            // sidesteps the whole class of bug. EncoderProfiles is small
            // (~50 rows max in practice), so the full scan is cheap.
            HashSet<Ulid> allExistingIds = (
                await context.EncoderProfiles.AsNoTracking().Select(p => p.Id).ToListAsync()
            ).ToHashSet();
            HashSet<Ulid> existingIds = materialized
                .Select(m => m.Id)
                .Where(allExistingIds.Contains)
                .ToHashSet();

            Logger.Setup(
                $"MaterializePresets DIAG: DB has {allExistingIds.Count} existing rows, "
                    + $"{existingIds.Count} of {materialized.Count} incoming match. "
                    + $"Sample DB id: {allExistingIds.FirstOrDefault()}, "
                    + $"sample incoming id: {materialized.FirstOrDefault()?.Id.ToString() ?? "(none)"}"
            );

            int inserted = 0;
            int updated = 0;

            foreach (EncoderProfile incoming in materialized)
            {
                if (existingIds.Contains(incoming.Id))
                {
                    EncoderProfile? tracked = await context.EncoderProfiles.FirstOrDefaultAsync(p =>
                        p.Id == incoming.Id
                    );
                    if (tracked is null)
                        continue;
                    tracked.Name = incoming.Name;
                    tracked.Container = incoming.Container;
                    tracked._videoProfiles = incoming._videoProfiles;
                    tracked._audioProfiles = incoming._audioProfiles;
                    tracked._subtitleProfiles = incoming._subtitleProfiles;
                    tracked._thumbnailProfile = incoming._thumbnailProfile;
                    updated++;
                }
                else
                {
                    context.EncoderProfiles.Add(incoming);
                    inserted++;
                }
            }

            await context.SaveChangesAsync();

            Logger.Setup(
                $"MaterializePresets: {inserted} inserted, {updated} updated EncoderProfile row(s) from EncodingPresets "
                    + $"(scanned={presets.Count} empty={skippedEmpty} "
                    + $"deserializeFailures={deserializeFailures} "
                    + $"deserializeNull={skippedDeserializeNull})"
            );
        }
        catch (Exception e)
        {
            Logger.Setup($"MaterializePresets: upsert failed: {e.Message}", LogEventLevel.Fatal);
        }
    }
}
