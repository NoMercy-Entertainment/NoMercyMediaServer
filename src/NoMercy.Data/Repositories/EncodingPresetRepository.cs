using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

/// <summary>
/// Repository for the shareable preset library. Presets are separate from
/// the runtime <c>EncoderProfile</c> table — applying a preset materializes
/// its resolved profile into a fresh EncoderProfile row, which is what the
/// encoder actually consumes. That decoupling means deleting or renaming a
/// preset never breaks an in-flight encode job.
/// </summary>
public class EncodingPresetRepository(MediaContext context)
{
    public Task<List<EncodingPreset>> ListAsync(int pageSize = 100, int pageIndex = 0)
    {
        if (pageSize <= 0)
            pageSize = 100;
        if (pageIndex < 0)
            pageIndex = 0;

        return context
            .EncodingPresets.AsNoTracking()
            .OrderBy(p => p.IsBuiltIn ? 0 : 1)
            .ThenBy(p => p.Name)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<EncodingPreset?> GetByIdAsync(Ulid id) =>
        context.EncodingPresets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    public Task<EncodingPreset?> GetByNameAsync(string name) =>
        context.EncodingPresets.AsNoTracking().FirstOrDefaultAsync(p => p.Name == name);

    public async Task<EncodingPreset> CreateAsync(EncodingPreset preset)
    {
        preset.CreatedAt = DateTime.UtcNow;
        preset.UpdatedAt = preset.CreatedAt;
        context.EncodingPresets.Add(preset);
        await context.SaveChangesAsync();
        return preset;
    }

    public async Task<EncodingPreset?> UpdateAsync(Ulid id, Action<EncodingPreset> apply)
    {
        EncodingPreset? existing = await context.EncodingPresets.FirstOrDefaultAsync(p =>
            p.Id == id
        );
        if (existing is null)
            return null;

        if (existing.IsBuiltIn)
            throw new InvalidOperationException(
                "Built-in presets cannot be modified in place — clone and edit instead."
            );

        apply(existing);
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Ulid id)
    {
        EncodingPreset? existing = await context.EncodingPresets.FirstOrDefaultAsync(p =>
            p.Id == id
        );
        if (existing is null)
            return false;

        if (existing.IsBuiltIn)
            throw new InvalidOperationException("Built-in presets cannot be deleted.");

        context.EncodingPresets.Remove(existing);
        await context.SaveChangesAsync();
        return true;
    }

    public Task<int> GetTotalCountAsync() => context.EncodingPresets.CountAsync();
}
