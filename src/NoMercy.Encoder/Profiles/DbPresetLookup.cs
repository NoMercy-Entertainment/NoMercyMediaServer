using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// <see cref="IPresetLookup"/> implementation that resolves V2 EncodingPreset
/// rows directly against <see cref="MediaContext"/>. Used by both the V1
/// resolver in the API layer and the V1 encode-job bridge so they walk the
/// parent chain through one shared adapter. Synchronous because
/// <see cref="PresetResolver"/> is a pure static method.
/// </summary>
public sealed class DbPresetLookup(MediaContext context) : IPresetLookup
{
    public (string ProfileJson, Ulid? ParentPresetId)? Get(Ulid presetId)
    {
        EncodingPreset? row = context
            .EncodingPresets.AsNoTracking()
            .FirstOrDefault(p => p.Id == presetId);
        return row is null ? null : (row.ProfileJson, row.ParentPresetId);
    }
}
