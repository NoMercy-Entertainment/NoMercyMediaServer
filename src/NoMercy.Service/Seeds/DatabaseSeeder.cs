using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Information;
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
        Logger.Setup("Initializing database schemas...");

        // 1. AppDbContext first — auth tokens live here, needed before content DB
        AppDbContext appDbContext = new();
        await Migrate(appDbContext);
        await EnsureDatabaseCreated(appDbContext);

        // Migrate Configuration data from media.db to app.db (one-time on update)
        await MigrateConfigurationData(appDbContext);
        await appDbContext.DisposeAsync();

        // 2. MediaContext — content and metadata
        MediaContext mediaDbContext = new();
        await Migrate(mediaDbContext);
        await EnsureDatabaseCreated(mediaDbContext);

        // 3. QueueContext — background jobs
        QueueContext queueDbContext = new();
        await Migrate(queueDbContext);
        await EnsureDatabaseCreated(queueDbContext);

        CronWorker.SignalDatabaseReady();
        Logger.Setup("Database schemas initialized");
    }

    private static async Task MigrateConfigurationData(AppDbContext appContext)
    {
        // Only migrate if app.db has no Configuration rows AND media.db exists with rows
        bool appHasData = await appContext.Configuration.AnyAsync();
        if (appHasData)
            return;

        string mediaDbPath = AppFiles.MediaDatabase;
        if (!File.Exists(mediaDbPath))
            return;

        try
        {
            // Check if media.db has a Configuration table with rows
            using SqliteConnection checkConn = new($"Data Source={mediaDbPath}");
            await checkConn.OpenAsync();

            using SqliteCommand checkCmd = checkConn.CreateCommand();
            checkCmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Configuration'";
            long tableExists = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L);

            if (tableExists == 0)
            {
                await checkConn.CloseAsync();
                return;
            }

            using SqliteCommand countCmd = checkConn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Configuration";
            long rowCount = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);

            await checkConn.CloseAsync();

            if (rowCount == 0)
                return;

            // Copy rows using ATTACH DATABASE on the app.db connection
            string appDbPath = AppFiles.AppDatabase;
            using SqliteConnection appConn = new($"Data Source={appDbPath}");
            await appConn.OpenAsync();

            using SqliteCommand attachCmd = appConn.CreateCommand();
            attachCmd.CommandText = $"ATTACH DATABASE '{mediaDbPath}' AS source";
            await attachCmd.ExecuteNonQueryAsync();

            using SqliteCommand copyCmd = appConn.CreateCommand();
            copyCmd.CommandText =
                "INSERT OR IGNORE INTO Configuration (Key, Value, ModifiedBy, CreatedAt, UpdatedAt) "
                + "SELECT Key, Value, ModifiedBy, CreatedAt, UpdatedAt FROM source.Configuration";
            int copied = await copyCmd.ExecuteNonQueryAsync();

            using SqliteCommand detachCmd = appConn.CreateCommand();
            detachCmd.CommandText = "DETACH DATABASE source";
            await detachCmd.ExecuteNonQueryAsync();

            await appConn.CloseAsync();

            Logger.Setup($"Migrated {copied} configuration rows from media.db to app.db");
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"Configuration migration from media.db failed (non-fatal): {ex.Message}",
                LogEventLevel.Warning
            );
        }
    }

    /// <summary>
    /// Seed truly offline data (config, encoder profiles, libraries).
    /// No network or auth required — safe to call right after InitSchema().
    /// Each seed is individually guarded so one failure doesn't block the rest.
    /// </summary>
    public static async Task SeedOfflineData()
    {
        AppDbContext appDbContext = new();
        MediaContext mediaDbContext = new();

        Func<Task>[] offlineSeeds =
        [
            () => ConfigSeed.Init(appDbContext),
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

            // Assign the owner to any seeded libraries that have no users yet
            await AssignOwnerToUnassignedLibraries(mediaDbContext);

            await ClaimsPrincipleExtensions.InitializeAsync(mediaDbContext);

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

    private static async Task AssignOwnerToUnassignedLibraries(MediaContext mediaContext)
    {
        try
        {
            User? owner = await mediaContext.Users.FirstOrDefaultAsync(u => u.Owner);
            if (owner is null)
                return;

            List<Ulid> assignedLibraryIds = await mediaContext
                .LibraryUser.Select(lu => lu.LibraryId)
                .Distinct()
                .ToListAsync();

            List<Library> unassigned = await mediaContext
                .Libraries.Where(l => !assignedLibraryIds.Contains(l.Id))
                .ToListAsync();

            if (unassigned.Count == 0)
                return;

            foreach (Library library in unassigned)
            {
                mediaContext.LibraryUser.Add(new LibraryUser(library.Id, owner.Id));
            }

            await mediaContext.SaveChangesAsync();
            Logger.Setup($"Assigned {unassigned.Count} libraries to owner {owner.Name}");
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"Failed to assign libraries to owner: {ex.Message}",
                LogEventLevel.Warning
            );
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
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
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
            Logger.Setup(
                $"{contextName}: Database is up to date. No migrations needed.",
                LogEventLevel.Verbose
            );
        }
        else
        {
            List<string> pendingMigrations = context.Database.GetPendingMigrations().ToList();

            if (pendingMigrations.Count > 0)
            {
                Logger.Setup(
                    $"{contextName}: Applying {pendingMigrations.Count} migration(s)...",
                    LogEventLevel.Verbose
                );
                try
                {
                    context.Database.Migrate();
                    Logger.Setup(
                        $"{contextName}: Migrations applied successfully.",
                        LogEventLevel.Verbose
                    );
                }
                catch (Exception ex) when (ex.Message.Contains("already exists"))
                {
                    Logger.Setup(
                        $"{contextName}: Tables already exist. Syncing migration history...",
                        LogEventLevel.Verbose
                    );
                    SyncMigrationHistory(
                        context,
                        migrationTableExists,
                        pendingMigrations,
                        availableMigrations
                    );
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

    private static void SyncMigrationHistory(
        DbContext context,
        bool migrationTableExists,
        List<string> pendingMigrations,
        List<string> availableMigrations
    )
    {
        string version = context.GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";

        // Always ensure the table exists — Migrate() may have partially created it before failing,
        // or it may already exist from a previous installation.
        if (!migrationTableExists)
        {
            context.Database.ExecuteSqlRaw(
                @"
                CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
                    MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
                    ProductVersion TEXT NOT NULL
                );"
            );
            Logger.Setup("Migration history table created.", LogEventLevel.Verbose);
        }

        // Mark all relevant migrations as applied — use OR IGNORE to skip duplicates.
        List<string> migrationsToRecord = migrationTableExists
            ? pendingMigrations
            : availableMigrations;
        foreach (string migration in migrationsToRecord)
        {
            try
            {
                context.Database.ExecuteSqlRaw(
                    "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})",
                    migration,
                    version
                );
                Logger.Setup($"Added migration {migration} to history", LogEventLevel.Verbose);
            }
            catch
            {
                Logger.Setup(
                    $"Failed to add migration {migration} to history",
                    LogEventLevel.Fatal
                );
            }
        }
    }

    private static async Task EnsureDatabaseCreated(DbContext context)
    {
        Logger.Setup(
            $"Ensuring database is created for {context.GetType().Name}",
            LogEventLevel.Verbose
        );
        await context.Database.EnsureCreatedAsync();
    }
}
