using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NoMercy.Database;

public class MediaContextDesignTimeFactory : IDesignTimeDbContextFactory<MediaContext>
{
    public MediaContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<MediaContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(
            "Data Source=media_designtime.db",
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
        );
        return new(optionsBuilder.Options);
    }
}
