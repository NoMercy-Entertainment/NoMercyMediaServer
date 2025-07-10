using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

public static class DatabaseSeeder
{
    internal static bool ShouldSeedMarvel { get; set; }
    
    public static async Task Run()
    {
        Logger.Setup("Starting database seeding process");
        
        MediaContext mediaDbContext = new();
        await using QueueContext queueDbContext = new();
        
        try
        {
            // Ensure database is created
            await EnsureDatabaseCreated(mediaDbContext);
            await Migrate(mediaDbContext);
            
            await EnsureDatabaseCreated(queueDbContext);
            await Migrate(queueDbContext);
            
            await ConfigSeed.Init(mediaDbContext);
            await LanguagesSeed.Init(mediaDbContext);
            await CountriesSeed.Init(mediaDbContext);
            await GenresSeed.Init(mediaDbContext);
            await CertificationsSeed.Init(mediaDbContext);
            await MusicGenresSeed.Init(mediaDbContext);
            await EncoderProfilesSeed.Init(mediaDbContext);
            await LibrariesSeed.Init(mediaDbContext);
            
            if (ShouldSeedMarvel)
            {
                Thread thread = new(() => _ = SpecialSeed.Init(mediaDbContext));
                thread.Start();
            }
            
            Logger.Setup("Successfully completed database seeding");
        }
        catch (Exception ex)
        {
            Logger.Setup(ex.Message, LogEventLevel.Error);
        }
    }
    
    private static Task Migrate(DbContext context)
    {
        try
        {
            // Check if migration is needed
            if (context?.Database.GetPendingMigrations().Any() == true)
            {
                context.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
                context.Database.ExecuteSqlRaw("PRAGMA encoding = 'UTF-8'");
                context.Database.Migrate();
                Console.WriteLine("Media database migrations applied.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying migrations: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
    
    private static async Task EnsureDatabaseCreated(DbContext context)
    {
        try
        {
            Logger.Setup($"Ensuring database is created for {context.GetType().Name}");
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Error);
        }
    }
}
