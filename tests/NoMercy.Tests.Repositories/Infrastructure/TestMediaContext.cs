using Microsoft.EntityFrameworkCore;
using NoMercy.Database;

namespace NoMercy.Tests.Repositories.Infrastructure;

public class TestMediaContext : MediaContext
{
    public TestMediaContext(DbContextOptions<MediaContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            base.OnConfiguring(options);
        }
    }
}
