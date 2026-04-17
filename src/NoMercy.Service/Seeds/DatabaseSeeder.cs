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
            () => EncodingPresetsSeed.Init(mediaDbContext),
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
    /// Seed provider data (TMDB genres, languages, certifications, etc.).
    /// Requires API keys (no auth). Called early in startup before any import jobs.
    /// </summary>
    public static async Task Run()
    {
        MediaContext mediaDbContext = new();

        await SeedOfflineData();

        Func<Task>[] seeds =
        [
            () => LanguagesSeed.Init(mediaDbContext),
            () => CountriesSeed.Init(mediaDbContext),
            () => GenresSeed.Init(mediaDbContext),
            () => CertificationsSeed.Init(mediaDbContext),
            () => MusicGenresSeed.Init(mediaDbContext),
        ];

        foreach (Func<Task> seed in seeds)
        {
            try
            {
                await seed();
            }
            catch (Exception ex)
            {
                Logger.Setup($"Seed failed: {ex.Message}", LogEventLevel.Warning);
            }
        }
    }

    /// <summary>
    /// Seed auth-dependent data (users, library assignment, claims).
    /// Called after auth completes via BootOrchestrator.
    /// </summary>
    public static async Task SeedAuthData()
    {
        MediaContext mediaDbContext = new();

        Func<Task>[] seeds =
        [
            () => UsersSeed.Init(mediaDbContext),
            () => AssignOwnerToUnassignedLibraries(mediaDbContext),
            () => ClaimsPrincipleExtensions.InitializeAsync(mediaDbContext),
        ];

        foreach (Func<Task> seed in seeds)
        {
            try
            {
                await seed();
            }
            catch (Exception ex)
            {
                Logger.Setup($"Auth seed failed: {ex.Message}", LogEventLevel.Warning);
            }
        }

        if (ShouldSeedMarvel)
        {
            Thread thread = new(() => _ = SpecialSeed.Init(mediaDbContext));
            thread.Start();
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

        // Self-heal: if the migration-history table says a migration has been
        // applied but its physical tables are missing, EF's GetPendingMigrations()
        // returns empty and we skip Migrate(). That's how a half-applied
        // schema gets stuck. Scan the applied list for migrations whose
        // expected tables don't exist and unstamp them so the next
        // GetPendingMigrations() reports them as pending.
        if (migrationTableExists)
        {
            UnstampMigrationsMissingTables(context, contextName);
        }

        // Use pending list as the source of truth — count equality was too
        // weak (same count by coincidence silently skipped new migrations).
        List<string> pendingMigrations = migrationTableExists
            ? context.Database.GetPendingMigrations().ToList()
            : availableMigrations;

        if (pendingMigrations.Count == 0)
        {
            Logger.Setup(
                $"{contextName}: Database is up to date ({availableMigrations.Count} migrations applied).",
                LogEventLevel.Verbose
            );
        }
        else
        {
            Logger.Setup(
                $"{contextName}: Applying {pendingMigrations.Count} migration(s): {string.Join(", ", pendingMigrations)}",
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

        // Configure SQLite pragmas after schema exists
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
        context.Database.ExecuteSqlRaw("PRAGMA encoding = 'UTF-8'");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Detects migrations that are marked as applied in __EFMigrationsHistory
    /// but whose <c>CREATE TABLE</c> statements never actually ran — the
    /// physical table is missing. Unstamps those rows so the next
    /// <c>Migrate()</c> call sees them as pending and applies them.
    ///
    /// How this state happens: a prior SyncMigrationHistory call (triggered
    /// by an "already exists" catch on an unrelated migration) can mark
    /// every pending migration as applied even though only one of them
    /// actually ran. Future runs then short-circuit because history says
    /// "all applied."
    /// </summary>
    private static void UnstampMigrationsMissingTables(DbContext context, string contextName)
    {
        IReadOnlyDictionary<string, string> migrationTables = GetExpectedTablesPerMigration(
            context
        );
        if (migrationTables.Count == 0)
            return;

        HashSet<string> existingTables = GetExistingTables(context);
        List<string> appliedMigrations = context.Database.GetAppliedMigrations().ToList();

        List<string> toUnstamp = [];
        foreach (string migrationId in appliedMigrations)
        {
            if (!migrationTables.TryGetValue(migrationId, out string? expectedTable))
                continue;
            if (string.IsNullOrEmpty(expectedTable))
                continue;
            if (existingTables.Contains(expectedTable))
                continue;

            toUnstamp.Add(migrationId);
        }

        if (toUnstamp.Count == 0)
            return;

        Logger.Setup(
            $"{contextName}: Detected {toUnstamp.Count} stamped-but-missing migration(s), "
                + $"unstamping so they re-apply: {string.Join(", ", toUnstamp)}",
            LogEventLevel.Warning
        );

        foreach (string migrationId in toUnstamp)
        {
            try
            {
                context.Database.ExecuteSqlRaw(
                    "DELETE FROM __EFMigrationsHistory WHERE MigrationId = {0}",
                    migrationId
                );
            }
            catch (Exception ex)
            {
                Logger.Setup(
                    $"{contextName}: Could not unstamp {migrationId}: {ex.Message}",
                    LogEventLevel.Warning
                );
            }
        }
    }

    /// <summary>
    /// Walks the model snapshot to produce a map of <c>migrationId → primary
    /// table name</c>. We only need the table name a migration creates;
    /// if multiple tables land in one migration we track the first one
    /// (usually enough to detect the "stamped but not run" state).
    /// </summary>
    private static IReadOnlyDictionary<string, string> GetExpectedTablesPerMigration(
        DbContext context
    )
    {
        Dictionary<string, string> result = new();

        // We can't reliably ask EF for the Up/Down operations per migration
        // without reflection on generated types. Instead keep a small curated
        // lookup for migrations whose primary table is the failure surface —
        // this list grows as new tables are added. The fallback is "unknown
        // migration" which we treat as safe (don't unstamp).
        result["20260416210105_AddEncodingHistoryTable"] = "EncodingHistory";
        result["20260417010426_AddEncodingPresetTable"] = "EncodingPresets";
        result["20260417011900_AddContentSegmentTable"] = "ContentSegments";

        // Context-type filtering: only return entries whose migration name
        // appears in the context's migration set.
        HashSet<string> known = context.Database.GetMigrations().ToHashSet();
        return result
            .Where(kv => known.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static HashSet<string> GetExistingTables(DbContext context)
    {
        HashSet<string> tables = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            System.Data.Common.DbConnection connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            using System.Data.Common.DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            using System.Data.Common.DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
                tables.Add(reader.GetString(0));
        }
        catch
        {
            // Best-effort — on failure we just skip the self-heal step.
        }

        return tables;
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
