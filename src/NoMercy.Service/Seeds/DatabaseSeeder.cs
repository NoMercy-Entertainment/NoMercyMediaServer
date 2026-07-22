// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NoMercy.Authorization;
using NoMercy.Database;
using NoMercy.Database.Maintenance;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.Database.Models.Users;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
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
    public static async Task InitSchema(IStorage storage)
    {
        Logger.Setup(message: "Initializing database schemas...");

        // 1. AppDbContext first — auth tokens live here, needed before content DB
        AppDbContext appDbContext = new();
        await Migrate(context: appDbContext);
        await EnsureDatabaseCreated(context: appDbContext);

        // Migrate Configuration data from media.db to app.db (one-time on update)
        await MigrateConfigurationData(appContext: appDbContext, storage: storage);

        // Resolve the server's stable identity BEFORE anything else reads
        // Info.DeviceId (auth, registration, DNS, certs). Must run after the
        // Configuration migration above so a pre-existing "device_identity_id"
        // or "ssl_certificate" row carried over from media.db is visible to it.
        Info.SetResolvedDeviceId(id: DeviceIdentityResolver.ResolveAndPersist(db: appDbContext));

        // Decide the default DNS scheme (apex vs srv) exactly once, before
        // BootOrchestrator's Phase 3 registration reads RuntimeServerSettings
        // and sends dns_scheme. Must run in this same pass so it can see the
        // same "prior registration" evidence (auth token / cert rows) the
        // migration above just made visible, and before Phase 2 auth mints a
        // brand-new token that would make a fresh install look pre-registered.
        DnsSchemeResolver.ResolveAndPersist(db: appDbContext);

        await appDbContext.DisposeAsync();

        // 2. MediaContext — content and metadata
        MediaContext mediaDbContext = new();
        await Migrate(context: mediaDbContext);
        await EnsureDatabaseCreated(context: mediaDbContext);

        // 3. QueueContext — background jobs
        QueueContext queueDbContext = new();
        await Migrate(context: queueDbContext);
        await EnsureDatabaseCreated(context: queueDbContext);

        CronWorker.SignalDatabaseReady();
        Logger.Setup(message: "Database schemas initialized");
    }

    private static async Task MigrateConfigurationData(AppDbContext appContext, IStorage storage)
    {
        // Only migrate if app.db has no Configuration rows AND media.db exists with rows
        bool appHasData = await appContext.Configuration.AnyAsync();
        if (appHasData)
            return;

        string mediaDbPath = AppFiles.MediaDatabase;
        if (!storage.Exists(path: mediaDbPath))
            return;

        try
        {
            // Check if media.db has a Configuration table with rows
            await using SqliteConnection checkConn = new(connectionString: $"Data Source={mediaDbPath}");
            await checkConn.OpenAsync();

            await using SqliteCommand checkCmd = checkConn.CreateCommand();
            checkCmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Configuration'";
            long tableExists = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L);

            if (tableExists == 0)
            {
                await checkConn.CloseAsync();
                return;
            }

            await using SqliteCommand countCmd = checkConn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Configuration";
            long rowCount = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);

            await checkConn.CloseAsync();

            if (rowCount == 0)
                return;

            // Copy rows using ATTACH DATABASE on the app.db connection
            string appDbPath = AppFiles.AppDatabase;
            await using SqliteConnection appConn = new(connectionString: $"Data Source={appDbPath}");
            await appConn.OpenAsync();

            await using SqliteCommand attachCmd = appConn.CreateCommand();
            attachCmd.CommandText = $"ATTACH DATABASE '{mediaDbPath}' AS source";
            await attachCmd.ExecuteNonQueryAsync();

            await using SqliteCommand copyCmd = appConn.CreateCommand();
            copyCmd.CommandText =
                "INSERT OR IGNORE INTO Configuration (Key, Value, ModifiedBy, CreatedAt, UpdatedAt) "
                + "SELECT Key, Value, ModifiedBy, CreatedAt, UpdatedAt FROM source.Configuration";
            int copied = await copyCmd.ExecuteNonQueryAsync();

            await using SqliteCommand detachCmd = appConn.CreateCommand();
            detachCmd.CommandText = "DETACH DATABASE source";
            await detachCmd.ExecuteNonQueryAsync();

            await appConn.CloseAsync();

            Logger.Setup(message: $"Migrated {copied} configuration rows from media.db to app.db");
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"Configuration migration from media.db failed (non-fatal): {ex.Message}",
                level: LogEventLevel.Warning
            );
        }
    }

    /// <summary>
    /// Seed truly offline data (config, encoder profiles, libraries).
    /// No network or auth required — safe to call right after InitSchema().
    /// Each seed is individually guarded so one failure doesn't block the rest.
    /// </summary>
    public static async Task SeedOfflineData(IStorage storage, IStorageDriver storageDriver)
    {
        AppDbContext appDbContext = new();
        MediaContext mediaDbContext = new();

        Func<Task>[] offlineSeeds =
        [
            () => appDbContext.Init(),
            () => SeedSystemLocalDriver(mediaContext: mediaDbContext),
            () => V1DriverBridgeSeed.RunAsync(context: mediaDbContext),
            () => LibrariesSeed.Init(dbContext: mediaDbContext, storage: storage, storageDriver: storageDriver),
            () => EncodingPresetsSeed.Init(context: mediaDbContext, storage: storage),
            () => LoadDiskOverlaysAsync(context: mediaDbContext),
        ];

        foreach (Func<Task> seed in offlineSeeds)
        {
            try
            {
                await seed();
            }
            catch (Exception ex)
            {
                Logger.Setup(message: $"Offline seed failed: {ex.Message}", level: LogEventLevel.Warning);
            }
        }
    }

    /// <summary>
    /// Seed provider data (TMDB genres, languages, certifications, etc.).
    /// Requires API keys (no auth). Called early in startup before any import jobs.
    /// </summary>
    public static async Task Run(IStorage storage, IStorageDriver storageDriver)
    {
        MediaContext mediaDbContext = new();

        await SeedOfflineData(storage: storage, storageDriver: storageDriver);

        Func<Task>[] seeds =
        [
            () => LanguagesSeed.Init(dbContext: mediaDbContext),
            () => CountriesSeed.Init(dbContext: mediaDbContext),
            () => GenresSeed.Init(dbContext: mediaDbContext),
            () => CertificationsSeed.Init(dbContext: mediaDbContext),
            () => MusicGenresSeed.Init(dbContext: mediaDbContext),
        ];

        foreach (Func<Task> seed in seeds)
        {
            try
            {
                await seed();
            }
            catch (Exception ex)
            {
                Logger.Setup(message: $"Seed failed: {ex.Message}", level: LogEventLevel.Warning);
            }
        }
    }

    public static async Task LoadDiskOverlaysAsync(MediaContext context)
    {
        string overlayDir = Path.Combine(path1: AppFiles.DataPath, path2: "profiles");
        Directory.CreateDirectory(path: overlayDir);

        DiskOverlayLoader.LoadResult overlay = DiskOverlayLoader.Load(directory: overlayDir);

        foreach (string error in overlay.Errors)
            Logger.Setup(message: $"Disk overlay load error: {error}", level: LogEventLevel.Warning);

        foreach (DiskOverlayLoader.LoadedPreset entry in overlay.Loaded)
        {
            EncodingProfile p = entry.Profile;
            Database.Models.Media.EncodingPreset? existing =
                await context.EncodingPresets.FirstOrDefaultAsync(predicate: x => x.Id == p.Id);

            if (existing is null)
            {
                context.EncodingPresets.Add(
                    entity: new()
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = entry.Description,
                        ProfileJson = Newtonsoft.Json.JsonConvert.SerializeObject(value: p),
                        ParentPresetId = entry.ParentPresetId,
                        IsBuiltIn = false,
                        Source = $"disk:{Path.GetFileName(path: entry.SourcePath)}",
                    }
                );
            }
            else if (existing.IsBuiltIn)
            {
                Logger.Setup(
                    message: $"Disk overlay '{entry.SourcePath}' has Ulid {p.Id} that collides with a built-in preset '{existing.Name}'. "
                             + "Built-ins are immutable; disk overlay rejected. Use a different Ulid to coexist.",
                    level: LogEventLevel.Warning
                );
            }
            else
            {
                existing.Name = p.Name;
                existing.Description = entry.Description ?? existing.Description;
                existing.ProfileJson = Newtonsoft.Json.JsonConvert.SerializeObject(value: p);
                existing.ParentPresetId = entry.ParentPresetId;
                existing.Source = $"disk:{Path.GetFileName(path: entry.SourcePath)}";
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Ensures the built-in system local driver row exists. Idempotent — skips
    /// if the row is already present. The driver uses an empty rootPath so
    /// StorageFactory builds each folder's guard from the folder's own subPath,
    /// giving per-folder path isolation without a separate driver per folder.
    /// </summary>
    public static async Task SeedSystemLocalDriver(MediaContext mediaContext)
    {
        bool exists = await mediaContext.Drivers.AnyAsync(predicate: d => d.Id == Driver.SystemLocalDriverId);
        if (exists)
            return;

        Driver systemLocalDriver = new()
        {
            Id = Driver.SystemLocalDriverId,
            Name = "Local",
            Type = "local",
            Config = "{\"rootPath\":\"\"}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        mediaContext.Drivers.Add(entity: systemLocalDriver);
        await mediaContext.SaveChangesAsync();

        Logger.Setup(message: "Seeded system local driver", level: LogEventLevel.Verbose);
    }

    /// <summary>
    /// Runs the bundle-slug rename pass after DI is available. Must be called
    /// after <see cref="Run"/> so the <see cref="IStorageFactory"/> singleton
    /// is fully configured with driver config resolvers.
    /// </summary>
    public static async Task RunBundleSlugRenamePassAsync(
        IStorageFactory storageFactory,
        ILogger<BundleSlugRenamer> logger
    )
    {
        if (BuiltinPresetRenames.SlugRenames.Count == 0)
            return;

        try
        {
            MediaContext context = new();
            BundleSlugRenamer renamer = new(
                slugMap: BuiltinPresetRenames.SlugRenames,
                storageFactory: storageFactory,
                context: context,
                logger: logger
            );
            await renamer.RunAsync();
            Logger.Setup(message: "Bundle slug rename pass complete", level: LogEventLevel.Verbose);
        }
        catch (Exception ex)
        {
            Logger.Setup(message: $"Bundle slug rename pass failed: {ex.Message}", level: LogEventLevel.Warning);
        }
    }

    /// <summary>
    /// Seed auth-dependent data (users, library assignment, claims).
    /// Called after auth completes via BootOrchestrator.
    /// </summary>
    public static async Task SeedAuthData(IStorage storage, string? accessToken)
    {
        MediaContext mediaDbContext = new();

        Func<Task>[] seeds =
        [
            () => UsersSeed.Init(dbContext: mediaDbContext, storage: storage, accessToken: accessToken),
            () => AssignOwnerToUnassignedLibraries(mediaContext: mediaDbContext),
            () => UserCache.Current.InitializeAsync(context: mediaDbContext),
        ];

        if (ShouldSeedMarvel)
        {
            try
            {
                await using MediaContext specialSeedContext = new();
                await SpecialSeed.Init(context: specialSeedContext);
            }
            catch (Exception ex)
            {
                Logger.Setup(message: $"Special seed failed: {ex.Message}", level: LogEventLevel.Warning);
            }
        }

        foreach (Func<Task> seed in seeds)
        {
            try
            {
                await seed();
            }
            catch (Exception ex)
            {
                Logger.Setup(message: $"Auth seed failed: {ex.Message}", level: LogEventLevel.Warning);
            }
        }
    }

    private static async Task AssignOwnerToUnassignedLibraries(MediaContext mediaContext)
    {
        try
        {
            User? owner = await mediaContext.Users.FirstOrDefaultAsync(predicate: u => u.Owner);
            if (owner is null)
                return;

            List<Ulid> assignedLibraryIds = await mediaContext
                .LibraryUser.Select(selector: lu => lu.LibraryId)
                .Distinct()
                .ToListAsync();

            List<Library> unassigned = await mediaContext
                .Libraries.Where(predicate: l => !assignedLibraryIds.Contains(l.Id))
                .ToListAsync();

            if (unassigned.Count == 0)
                return;

            foreach (Library library in unassigned)
            {
                mediaContext.LibraryUser.Add(entity: new(libraryId: library.Id, userId: owner.Id));
            }

            await mediaContext.SaveChangesAsync();
            Logger.Setup(message: $"Assigned {unassigned.Count} libraries to owner {owner.Name}");
        }
        catch (Exception ex)
        {
            Logger.Setup(
                message: $"Failed to assign libraries to owner: {ex.Message}",
                level: LogEventLevel.Warning
            );
        }
    }

    private static Task Migrate(DbContext context)
    {
        string contextName = context.GetType().Name;

        // Check if migration history table exists to determine DB state.
        // Do NOT run PRAGMA commands first — they create an empty .db file
        // which causes CanConnect() to return true on a fresh installation.
        // NOTE: Must use raw ADO.NET here — ExecuteSqlRaw returns rows-affected (-1 for SELECT),
        // not the query result, so it can't be used to read a scalar value.
        bool migrationTableExists = false;
        try
        {
            DbConnection connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                connection.Open();
            using DbCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
            migrationTableExists = Convert.ToInt64(value: command.ExecuteScalar()) > 0;
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
            UnstampMigrationsMissingTables(context: context, contextName: contextName);
        }

        // Use pending list as the source of truth — count equality was too
        // weak (same count by coincidence silently skipped new migrations).
        List<string> pendingMigrations = migrationTableExists
            ? context.Database.GetPendingMigrations().ToList()
            : availableMigrations;

        if (pendingMigrations.Count == 0)
        {
            Logger.Setup(
                message: $"{contextName}: Database is up to date ({availableMigrations.Count} migrations applied).",
                level: LogEventLevel.Verbose
            );
        }
        else
        {
            Logger.Setup(
                message: $"{contextName}: Applying {pendingMigrations.Count} migration(s): {string.Join(separator: ", ", values: pendingMigrations)}",
                level: LogEventLevel.Verbose
            );

            string? dbPath = context.Database.GetDbConnection().DataSource;
            if (!string.IsNullOrEmpty(value: dbPath))
                DatabaseBackupService.BackupBeforeMigration(dbPath: dbPath, pendingMigrationCount: pendingMigrations.Count);

            // Rows whose parent was deleted before cascade rules existed make EF's
            // SQLite table-rebuild migrations throw "FOREIGN KEY constraint failed"
            // when they copy the orphan. Clear them first (the backup above is the
            // safety net) so those migrations can apply. Best-effort: a cleanup
            // failure must not block the migration — the catch below still reports
            // any orphan that survives.
            try
            {
                IReadOnlyDictionary<string, int> removedOrphans = ForeignKeyOrphanCleaner.Clean(
                    connection: context.Database.GetDbConnection(),
                    contextName: contextName
                );
                if (removedOrphans.Count > 0)
                    Logger.Setup(
                        message: $"{contextName}: Removed {removedOrphans.Values.Sum()} foreign-key-orphaned row(s) before migration: "
                                 + string.Join(
                                     separator: ", ",
                                     values: removedOrphans.Select(selector: entry => $"{entry.Key}={entry.Value}")
                                 ),
                        level: LogEventLevel.Warning
                    );
            }
            catch (Exception ex)
            {
                Logger.Setup(
                    message: $"{contextName}: Orphan pre-flight failed (non-fatal): {ex.Message}",
                    level: LogEventLevel.Warning
                );
            }

            try
            {
                context.Database.Migrate();
                Logger.Setup(
                    message: $"{contextName}: Migrations applied successfully.",
                    level: LogEventLevel.Verbose
                );
            }
            catch (Exception ex) when (ex.Message.Contains(value: "already exists"))
            {
                Logger.Setup(
                    message: $"{contextName}: Tables already exist. Syncing migration history...",
                    level: LogEventLevel.Verbose
                );
                SyncMigrationHistory(
                    context: context,
                    migrationTableExists: migrationTableExists,
                    pendingMigrations: pendingMigrations,
                    availableMigrations: availableMigrations
                );
            }
            catch (Exception ex) when (ex.Message.Contains(value: "FOREIGN KEY constraint failed"))
            {
                IReadOnlyList<string> violations = ForeignKeyOrphanCleaner.DescribeViolations(
                    connection: context.Database.GetDbConnection()
                );
                Logger.Setup(
                    message: $"{contextName}: Migration failed on a foreign-key constraint. "
                             + (
                                 violations.Count > 0
                                     ? $"Orphaned rows: {string.Join(separator: "; ", values: violations)}"
                                     : "No pre-existing orphans remain — the violation is in a migration's own data step."
                             ),
                    level: LogEventLevel.Fatal
                );
                throw;
            }
        }

        // Configure SQLite pragmas after schema exists
        context.Database.ExecuteSqlRaw(sql: "PRAGMA journal_mode = WAL;");
        context.Database.ExecuteSqlRaw(sql: "PRAGMA encoding = 'UTF-8'");

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
            context: context
        );
        if (migrationTables.Count == 0)
            return;

        HashSet<string> existingTables = GetExistingTables(context: context);
        List<string> appliedMigrations = context.Database.GetAppliedMigrations().ToList();

        List<string> toUnstamp = [];
        foreach (string migrationId in appliedMigrations)
        {
            if (!migrationTables.TryGetValue(key: migrationId, value: out string? expectedTable))
                continue;
            if (string.IsNullOrEmpty(value: expectedTable))
                continue;
            if (existingTables.Contains(item: expectedTable))
                continue;

            toUnstamp.Add(item: migrationId);
        }

        if (toUnstamp.Count == 0)
            return;

        Logger.Setup(
            message: $"{contextName}: Detected {toUnstamp.Count} stamped-but-missing migration(s), "
                     + $"unstamping so they re-apply: {string.Join(separator: ", ", values: toUnstamp)}",
            level: LogEventLevel.Warning
        );

        foreach (string migrationId in toUnstamp)
        {
            try
            {
                context.Database.ExecuteSqlRaw(
                    sql: "DELETE FROM __EFMigrationsHistory WHERE MigrationId = {0}",
                    parameters: migrationId
                );
            }
            catch (Exception ex)
            {
                Logger.Setup(
                    message: $"{contextName}: Could not unstamp {migrationId}: {ex.Message}",
                    level: LogEventLevel.Warning
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
        Dictionary<string, string> result = new()
        {
            // We can't reliably ask EF for the Up/Down operations per migration
            // without reflection on generated types. Instead, keep a small curated
            // lookup for migrations whose primary table is the failure surface —
            // this list grows as new tables are added. The fallback is "unknown
            // migration" which we treat as safe (don't unstamp).
            [key: "20260416210105_AddEncodingHistoryTable"] = "EncodingHistory",
            [key: "20260417010426_AddEncodingPresetTable"] = "EncodingPresets",
            [key: "20260417011900_AddContentSegmentTable"] = "ContentSegments",
        };

        // Context-type filtering: only return entries whose migration name
        // appears in the context's migration set.
        HashSet<string> known = context.Database.GetMigrations().ToHashSet();
        return result
            .Where(predicate: kv => known.Contains(item: kv.Key))
            .ToDictionary(keySelector: kv => kv.Key, elementSelector: kv => kv.Value);
    }

    private static HashSet<string> GetExistingTables(DbContext context)
    {
        HashSet<string> tables = new(comparer: StringComparer.OrdinalIgnoreCase);

        try
        {
            DbConnection connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                connection.Open();

            using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            using DbDataReader reader = command.ExecuteReader();
            while (reader.Read())
                tables.Add(item: reader.GetString(ordinal: 0));
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
                sql: @"
                CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
                    MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
                    ProductVersion TEXT NOT NULL
                );"
            );
            Logger.Setup(message: "Migration history table created.", level: LogEventLevel.Verbose);
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
                    sql: "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})", parameters: [migration, version]
                );
                Logger.Setup(message: $"Added migration {migration} to history", level: LogEventLevel.Verbose);
            }
            catch
            {
                Logger.Setup(
                    message: $"Failed to add migration {migration} to history",
                    level: LogEventLevel.Fatal
                );
            }
        }
    }

    private static async Task EnsureDatabaseCreated(DbContext context)
    {
        Logger.Setup(
            message: $"Ensuring database is created for {context.GetType().Name}",
            level: LogEventLevel.Verbose
        );
        await context.Database.EnsureCreatedAsync();
    }
}
