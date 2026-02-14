using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Queue.Workers;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class DatabaseSeeder
{
    internal static bool ShouldSeedMarvel { get; set; }
    
    public static async Task Run()
    {
        Logger.Setup("Starting database seeding process", LogEventLevel.Verbose);
        
        MediaContext mediaDbContext = new();
        await using QueueContext queueDbContext = new();
        
        try
        {
            // Ensure database is created
            await Migrate(mediaDbContext);
            await EnsureDatabaseCreated(mediaDbContext);
            
            await Migrate(queueDbContext);
            await EnsureDatabaseCreated(queueDbContext);

            // Signal CronWorker that the database is migrated and ready for queries
            CronWorker.SignalDatabaseReady();

            await ConfigSeed.Init(mediaDbContext);
            await LanguagesSeed.Init(mediaDbContext);
            await CountriesSeed.Init(mediaDbContext);
            await GenresSeed.Init(mediaDbContext);
            await CertificationsSeed.Init(mediaDbContext);
            await MusicGenresSeed.Init(mediaDbContext);
            await LibrariesSeed.Init(mediaDbContext);
            await EncoderProfilesSeed.Init(mediaDbContext);
            await UsersSeed.Init(mediaDbContext);

            ClaimsPrincipleExtensions.Initialize(mediaDbContext);

            if (ShouldSeedMarvel)
            {
                Thread thread = new(() => _ = SpecialSeed.Init(mediaDbContext));
                thread.Start();
            }
            
            Logger.Setup("Successfully completed database seeding", LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            CronWorker.SignalDatabaseReady(false);
            Logger.Setup(ex.Message, LogEventLevel.Fatal);
        }
    }
    
    private static Task Migrate(DbContext context)
    {
        try
        {
            // Configure SQLite for better performance and UTF-8 support
            context.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
            context.Database.ExecuteSqlRaw("PRAGMA encoding = 'UTF-8'");
            
            // First check if the database exists - if not, create it
            bool dbExists = context.Database.CanConnect();
            
            if (!dbExists)
            {
                Logger.Setup("Database doesn't exist. Creating database and applying migrations...", LogEventLevel.Verbose);
                context.Database.Migrate();
            }
            else
            {
                // Check if migration history table exists and has the correct records
                bool migrationTableExists;
                try
                {
                    migrationTableExists = context.Database
                        .ExecuteSqlRaw("SELECT COUNT(*) FROM __EFMigrationsHistory") >= 0;
                }
                catch
                {
                    migrationTableExists = false;
                }
                
                // Get list of applied migrations in the database
                List<string> appliedMigrations = [];
                if (migrationTableExists)
                {
                    appliedMigrations = context.Database.GetAppliedMigrations().ToList();
                }
                
                // Get list of available migrations in code
                List<string> availableMigrations = context.Database.GetMigrations().ToList();
                
                if (migrationTableExists && appliedMigrations.Count == availableMigrations.Count)
                {
                    Logger.Setup("Database is up to date. No migrations needed.", LogEventLevel.Verbose);
                }
                else
                {
                    Logger.Setup("Checking for pending migrations...", LogEventLevel.Verbose);
                    bool hasPendingMigrations = context.Database.GetPendingMigrations().Any();
                    
                    if (hasPendingMigrations)
                    {
                        try
                        {
                            context.Database.Migrate();
                            Logger.Setup("Migrations applied successfully.", LogEventLevel.Verbose);
                        }
                        catch (Exception ex) when (ex.Message.Contains("already exists"))
                        {
                            Logger.Setup("Tables already exist. Ensuring migration history is up to date...", LogEventLevel.Verbose);
                            
                            try
                            {
                                if (migrationTableExists)
                                {
                                    // Don't delete - just ensure all migrations are recorded
                                    List<string> pendingMigrations = context.Database.GetPendingMigrations().ToList();
                                    string version = context.GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
                                    
                                    foreach (string migration in pendingMigrations)
                                    {
                                        try
                                        {
                                            context.Database.ExecuteSqlRaw(
                                                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})", 
                                                migration, 
                                                version);
                                            Logger.Setup($"Added migration {migration} to history", LogEventLevel.Verbose);
                                        }
                                        catch
                                        {
                                            Logger.Setup($"Failed to add migration {migration} to history", LogEventLevel.Fatal);
                                        }
                                    }
                                }
                                else
                                {
                                    // Create the migrations history table
                                    context.Database.ExecuteSqlRaw(@"
                                        CREATE TABLE __EFMigrationsHistory (
                                            MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
                                            ProductVersion TEXT NOT NULL
                                        );");
                                    
                                    // Add all migrations to history
                                    string version = context.GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
                                    foreach (string migration in availableMigrations)
                                    {
                                        context.Database.ExecuteSqlRaw(
                                            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})", 
                                            migration, 
                                            version);
                                    }
                                    Logger.Setup("Migration history table created and populated.", LogEventLevel.Verbose);
                                }
                            }
                            catch (Exception innerEx)
                            {
                                Logger.Setup($"Failed to update migration history: {innerEx.Message}", LogEventLevel.Fatal);
                            }
                        }
                    }
                    else
                    {
                        Logger.Setup("No pending migrations found.", LogEventLevel.Verbose);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying migrations: {ex.Message}", LogEventLevel.Fatal);
        }
        
        return Task.CompletedTask;
    }
    
    private static async Task EnsureDatabaseCreated(DbContext context)
    {
        try
        {
            Logger.Setup($"Ensuring database is created for {context.GetType().Name}", LogEventLevel.Verbose);
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
