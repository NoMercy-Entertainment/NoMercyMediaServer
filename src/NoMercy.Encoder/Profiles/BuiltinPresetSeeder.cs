namespace NoMercy.Encoder.Profiles;

using Database;
using Database.Models.Media;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using V2BuiltinPresets = BuiltinPresets;
using V2EncodingProfile = EncodingProfile;

public class BuiltinPresetSeeder(MediaContext context)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        V2EncodingProfile[] builtins = V2BuiltinPresets.All();
        HashSet<Ulid> builtinIds = builtins.Select(p => p.Id).ToHashSet();

        foreach (V2EncodingProfile profile in builtins)
        {
            string profileJson = JsonConvert.SerializeObject(profile);
            EncodingPreset? existing = await context.EncodingPresets.FirstOrDefaultAsync(
                p => p.Id == profile.Id,
                ct
            );

            if (existing is null)
            {
                context.EncodingPresets.Add(
                    new()
                    {
                        Id = profile.Id,
                        Name = profile.Name,
                        Description = profile.Description,
                        ProfileJson = profileJson,
                        IsBuiltIn = true,
                        Source = "builtin",
                    }
                );
            }
            else
            {
                existing.Name = profile.Name;
                existing.Description = profile.Description;
                existing.ProfileJson = profileJson;
                existing.IsBuiltIn = true;
                existing.Source = "builtin";
            }
        }

        // Drop built-in rows whose Ulids no longer match shipped builtins
        // (rename/remove handling). Materialize once, then filter in-memory.
        List<EncodingPreset> currentBuiltins = await context
            .EncodingPresets.Where(p => p.IsBuiltIn)
            .ToListAsync(ct);

        foreach (EncodingPreset stale in currentBuiltins.Where(p => !builtinIds.Contains(p.Id)))
        {
            context.EncodingPresets.Remove(stale);
        }

        await context.SaveChangesAsync(ct);
    }
}
