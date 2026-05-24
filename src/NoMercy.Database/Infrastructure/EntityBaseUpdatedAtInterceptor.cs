using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NoMercy.Database;

public class EntityBaseUpdatedAtInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new()
    )
    {
        if (eventData.Context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        IEnumerable<Timestamps> entries = eventData
            .Context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .OfType<Timestamps>();

        foreach (Timestamps entry in entries)
        {
            // CreatedAt comes from SQLite's CURRENT_TIMESTAMP (UTC). Mixing
            // DateTime.Now here introduced a TZ drift on every save.
            entry.UpdatedAt = DateTime.UtcNow;
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
