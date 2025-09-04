using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NoMercy.Database;

public class EntityBaseUpdatedAtInterceptor : SaveChangesInterceptor
{   
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        IEnumerable<Timestamps> entries = eventData.Context.ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .Cast<Timestamps>();

        foreach (Timestamps entry in entries)
        {
            entry.UpdatedAt = DateTime.Now;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}