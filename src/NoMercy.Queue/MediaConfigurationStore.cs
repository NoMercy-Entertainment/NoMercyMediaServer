using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Queue.Core.Interfaces;

namespace NoMercy.Queue;

public class MediaConfigurationStore : IConfigurationStore
{
    public string? GetValue(string key)
    {
        using MediaContext context = new();
        Configuration? config = context.Configuration.FirstOrDefault(c => c.Key == key);
        return config?.Value;
    }

    public void SetValue(string key, string value)
    {
        using MediaContext context = new();
        Configuration? existing = context.Configuration.FirstOrDefault(c => c.Key == key);
        if (existing is not null)
        {
            existing.Value = value;
        }
        else
        {
            context.Configuration.Add(new Configuration { Key = key, Value = value });
        }
        context.SaveChanges();
    }

    public async Task SetValueAsync(string key, string value, Guid? modifiedBy = null)
    {
        await using MediaContext context = new();
        Configuration? existing = await context.Configuration.FirstOrDefaultAsync(c => c.Key == key);
        if (existing is not null)
        {
            existing.Value = value;
            existing.ModifiedBy = modifiedBy;
        }
        else
        {
            context.Configuration.Add(new Configuration { Key = key, Value = value, ModifiedBy = modifiedBy });
        }
        await context.SaveChangesAsync();
    }

    public bool HasKey(string key)
    {
        using MediaContext context = new();
        return context.Configuration.Any(c => c.Key == key);
    }
}
