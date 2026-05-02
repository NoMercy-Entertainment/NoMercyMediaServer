using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Hosted service that upserts the 12 built-in encoding presets into the
/// <c>EncodingPresets</c> table on every server start.
///
/// Rules:
///   - Absent row          → insert with IsBuiltIn = true.
///   - Present + IsBuiltIn → upsert (overwrite ProfileJson / Description;
///                           folder assignments on the row are untouched
///                           because the seeder never touches navigation props).
///   - Present + !IsBuiltIn → skip. A user removed the flag deliberately;
///                            never clobber their row.
///
/// <para>Takes <see cref="IServiceScopeFactory"/> rather than
/// <c>MediaContext</c> directly because DbContext is scoped and this
/// hosted service runs as a singleton — mixing the two trips the
/// container's lifetime validator. Each StartAsync call opens its own
/// scope, drains the work, and disposes.</para>
/// </summary>
public sealed class BuiltinPresetSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<BuiltinPresetSeeder> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await SeedAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown — fall through.
        }
        catch (Exception ex)
        {
            // Don't crash the host on a seeder failure: missing presets just
            // means the dashboard list starts empty, the user can still ship
            // their own profile. .NET 6+ defaults to StopHost on a hosted
            // service throw, which is the wrong tradeoff for cosmetic seed.
            logger.LogError(ex, "BuiltinPresetSeeder failed; continuing without preset upserts");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        MediaContext context = scope.ServiceProvider.GetRequiredService<MediaContext>();

        IReadOnlyList<EncodingProfile> presets = BuiltinPresets.All();

        int inserted = 0;
        int updated = 0;
        int skipped = 0;

        foreach (EncodingProfile preset in presets)
        {
            string json = JsonConvert.SerializeObject(preset);

            EncodingPreset? existing = await context.EncodingPresets.FindAsync([preset.Id], ct);

            if (existing is null)
            {
                EncodingPreset row = new()
                {
                    Id = preset.Id,
                    Name = preset.Name,
                    Description = preset.Description,
                    ProfileJson = json,
                    IsBuiltIn = true,
                    Author = "NoMercy",
                    Tags = "builtin",
                };
                context.EncodingPresets.Add(row);
                inserted++;
                continue;
            }

            if (!existing.IsBuiltIn)
            {
                logger.LogWarning(
                    "Builtin preset '{Name}' ({Id}) exists in the database but IsBuiltIn is false — "
                        + "the user may have modified it. Skipping to avoid data loss.",
                    preset.Name,
                    preset.Id
                );
                skipped++;
                continue;
            }

            // Present and still marked builtin — overwrite JSON + metadata
            // but leave user-assigned folder relationships intact.
            existing.Name = preset.Name;
            existing.Description = preset.Description;
            existing.ProfileJson = json;
            existing.UpdatedAt = DateTime.UtcNow;
            updated++;
        }

        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "BuiltinPresetSeeder: {Inserted} inserted, {Updated} updated, {Skipped} skipped",
            inserted,
            updated,
            skipped
        );
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
