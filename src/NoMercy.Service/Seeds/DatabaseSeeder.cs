using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercyQueue.Workers;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class DatabaseSeeder
{
    internal static bool ShouldSeedMarvel { get; set; }

    /// <summary>
    /// Phase 1: Create database schema (migrations + EnsureCreated).
    /// Does NOT require authentication — safe to call before auth.
    /// </summary>
    public static async Task InitSchema()
    {
        Logger.Setup("Initializing database schema...", LogEventLevel.Verbose);

        MediaContext mediaDbContext = new();
        await using QueueContext queueDbContext = new();

        try
        {
            await Migrate(mediaDbContext);
            await EnsureDatabaseCreated(mediaDbContext);

            await Migrate(queueDbContext);
            await EnsureDatabaseCreated(queueDbContext);

            CronWorker.SignalDatabaseReady();
            Logger.Setup("Database schema initialized successfully", LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            CronWorker.SignalDatabaseReady(false);
            Logger.Setup($"Database schema initialization failed: {ex.Message}", LogEventLevel.Fatal);
            throw;
        }
    }

    /// <summary>
    /// Seed truly offline data (config, encoder profiles, libraries).
    /// No network or auth required — safe to call right after InitSchema().
    /// Each seed is individually guarded so one failure doesn't block the rest.
    /// </summary>
    public static async Task SeedOfflineData()
    {
        MediaContext mediaDbContext = new();

        Func<Task>[] offlineSeeds =
        [
            () => ConfigSeed.Init(mediaDbContext),
            () => LibrariesSeed.Init(mediaDbContext),
            () => EncoderProfilesSeed.Init(mediaDbContext),
        ];

        foreach (Func<Task> seed in offlineSeeds)
        {
            try
            {
                await seed();
            }
            catch (Exception ex)
            {
                Logger.Setup($"Offline seed failed: {ex.Message}", LogEventLevel.Warning);
            }
        }
    }

    /// <summary>
    /// Phase 3: Seed all remaining data (TMDB genres, languages, users, etc.).
    /// Requires network + auth — called after auth completes.
    /// Schema and offline data must already exist.
    /// </summary>
    public static async Task Run()
    {
        MediaContext mediaDbContext = new();

        try
        {
            // Re-run offline seeds to pick up any updates
            await SeedOfflineData();

            await LanguagesSeed.Init(mediaDbContext);
            await CountriesSeed.Init(mediaDbContext);
            await GenresSeed.Init(mediaDbContext);
            await CertificationsSeed.Init(mediaDbContext);
            await MusicGenresSeed.Init(mediaDbContext);
            await UsersSeed.Init(mediaDbContext);

            ClaimsPrincipleExtensions.Initialize(mediaDbContext);

            if (ShouldSeedMarvel)
            {
                Thread thread = new(() => _ = SpecialSeed.Init(mediaDbContext));
                thread.Start();
            }
        }
        catch (Exception ex)
        {
            Logger.Setup($"Database seeding failed: {ex.Message}", LogEventLevel.Warning);
        }
    }
    
    private static Task Migrate(DbContext context)
    {
        string contextName = context.GetType().Name;

        // Check if migration history table exists to determine DB state.
        // Do NOT run PRAGMA commands first — they create an empty .db file
        // which causes CanConnect() to return true on a fresh install.
        // NOTE: Must use raw ADO.NET here — ExecuteSqlRaw returns rows-affected (-1 for SELECT),
        // not the query result, so it can't be used to read a scalar value.
        bool migrationTableExists = false;
        try
        {
            System.Data.Common.DbConnection connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();
            using System.Data.Common.DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
            migrationTableExists = Convert.ToInt64(command.ExecuteScalar()) > 0;
        }
        catch
        {
            // Could not check — assume table doesn't exist
        }

        List<string> availableMigrations = context.Database.GetMigrations().ToList();
        List<string> appliedMigrations = migrationTableExists
            ? context.Database.GetAppliedMigrations().ToList()
            : [];

        if (migrationTableExists && appliedMigrations.Count == availableMigrations.Count)
        {
            Logger.Setup($"{contextName}: Database is up to date. No migrations needed.", LogEventLevel.Verbose);
        }
        else
        {
            List<string> pendingMigrations = context.Database.GetPendingMigrations().ToList();

            if (pendingMigrations.Count > 0)
            {
                Logger.Setup($"{contextName}: Applying {pendingMigrations.Count} migration(s)...", LogEventLevel.Verbose);
                try
                {
                    context.Database.Migrate();
                    Logger.Setup($"{contextName}: Migrations applied successfully.", LogEventLevel.Verbose);
                }
                catch (Exception ex) when (ex.Message.Contains("already exists"))
                {
                    Logger.Setup($"{contextName}: Tables already exist. Syncing migration history...", LogEventLevel.Verbose);
                    SyncMigrationHistory(context, migrationTableExists, pendingMigrations, availableMigrations);
                }
            }
            else
            {
                Logger.Setup($"{contextName}: No pending migrations found.", LogEventLevel.Verbose);
            }
        }

        // Configure SQLite pragmas after schema exists
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
        context.Database.ExecuteSqlRaw("PRAGMA encoding = 'UTF-8'");

        return Task.CompletedTask;
    }

    private static void SyncMigrationHistory(DbContext context, bool migrationTableExists,
        List<string> pendingMigrations, List<string> availableMigrations)
    {
        string version = context.GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";

        // Always ensure the table exists — Migrate() may have partially created it before failing,
        // or it may already exist from a previous installation.
        if (!migrationTableExists)
        {
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
                    MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
                    ProductVersion TEXT NOT NULL
                );");
            Logger.Setup("Migration history table created.", LogEventLevel.Verbose);
        }

        // Mark all relevant migrations as applied — use OR IGNORE to skip duplicates.
        List<string> migrationsToRecord = migrationTableExists ? pendingMigrations : availableMigrations;
        foreach (string migration in migrationsToRecord)
        {
            try
            {
                context.Database.ExecuteSqlRaw(
                    "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                    migration, version);
                Logger.Setup($"Added migration {migration} to history", LogEventLevel.Verbose);
            }
            catch
            {
                Logger.Setup($"Failed to add migration {migration} to history", LogEventLevel.Fatal);
            }
        }
    }
    
    private static async Task EnsureDatabaseCreated(DbContext context)
    {
        Logger.Setup($"Ensuring database is created for {context.GetType().Name}", LogEventLevel.Verbose);
        await context.Database.EnsureCreatedAsync();
    }
}
