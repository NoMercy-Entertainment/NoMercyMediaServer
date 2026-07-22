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

// ReSharper disable MemberCanBePrivate.Global

using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.Information;

public static class AppFiles
{
    public static readonly string ApplicationName = "NoMercy MediaServer";

    public static readonly string AppDataPath =
        Environment.OSVersion.Platform == PlatformID.Unix
            ? Path.Combine(
                path1: Environment.GetEnvironmentVariable(variable: "HOME") ?? "/home/current",
                path2: ".local/share"
            )
            : Environment.GetFolderPath(folder: Environment.SpecialFolder.LocalApplicationData);

    // An explicit NOMERCY_APP_PATH override lets each process (notably each
    // parallel test assembly) use a fully isolated app-data root — its own
    // database, cache and logs — so concurrent test processes never collide
    // on shared on-disk state. Unset in production, so behaviour is unchanged.
    public static string AppPath =>
        Environment.GetEnvironmentVariable(variable: "NOMERCY_APP_PATH") is { Length: > 0 } appPathOverride
            ? appPathOverride
        : Config.IsTest ? Path.Combine(path1: AppDataPath, path2: "NoMercy_test")
        : Config.IsDev ? Path.Combine(path1: AppDataPath, path2: "NoMercy_dev")
        : Path.Combine(path1: AppDataPath, path2: "NoMercy");

    // ── Config ───────────────────────────────────────────────────────────

    public static string ConfigPath => Path.Combine(path1: AppPath, path2: "config");

    [Obsolete(message: "Tokens are now stored encrypted in app.db. Kept for migration detection only.")]
    public static string TokenFile => Path.Combine(path1: ConfigPath, path2: "token.json");
    public static string AuthKeysFile => Path.Combine(path1: ConfigPath, path2: "auth_keys.json");
    public static string JwksCacheFile => Path.Combine(path1: ConfigPath, path2: "jwks_cache.json");
    public static string ApiKeysFile => Path.Combine(path1: ConfigPath, path2: "api_keys.json");
    public static string TraySettingsFile => Path.Combine(path1: ConfigPath, path2: "tray_settings.json");

    public static string SeedsPath => Path.Combine(path1: ConfigPath, path2: "seeds");
    public static string FolderRootsSeedFile => Path.Combine(path1: SeedsPath, path2: "folderRoots.jsonc");
    public static string LibrariesSeedFile => Path.Combine(path1: SeedsPath, path2: "libraries.jsonc");
    public static string EncoderProfilesSeedFile =>
        Path.Combine(path1: SeedsPath, path2: "encoderProfiles.jsonc");
    public static string EncodingPresetsSeedFile =>
        Path.Combine(path1: SeedsPath, path2: "encodingPresets.jsonc");

    // ── Data & Logs ──────────────────────────────────────────────────────

    public static string DataPath => Path.Combine(path1: AppPath, path2: "data");
    public static string LogPath => Path.Combine(path1: AppPath, path2: "log");

    // AES-128 HLS DRM keys, protected at rest via DataProtection (see
    // NoMercy.NmSystem.Security.DrmKeyStore). Never served as static files —
    // outside CachePath/TranscodePath so it can never land in a published
    // transcode output.
    public static string DrmKeysPath => Path.Combine(path1: DataPath, path2: "drm_keys");

    // ── Cache ────────────────────────────────────────────────────────────

    public static string CachePath => Path.Combine(path1: AppPath, path2: "cache");
    public static string ApiCachePath => Path.Combine(path1: CachePath, path2: "api");
    public static string TempPath => Path.Combine(path1: CachePath, path2: "temp");
    public static string TranscodePath => Path.Combine(path1: CachePath, path2: "transcode");
    public static string EncoderCachePath => Path.Combine(path1: CachePath, path2: "encoder");
    public static string ImagesPath => Path.Combine(path1: CachePath, path2: "images");
    public static string MusicImagesPath => Path.Combine(path1: ImagesPath, path2: "music");
    public static string TempImagesPath => Path.Combine(path1: ImagesPath, path2: "temp");

    // Encoder hardware speed-index cache. Stores per-encoder/codec/resolution
    // FPS measurements so reboots reuse the calibration instead of redoing
    // 20+ minutes of synthetic encodes on every start.
    public static string SpeedIndexCachePath =>
        Path.Combine(path1: CachePath, path2: "encoder", path3: "speed_index.json");

    // ── Browser ──────────────────────────────────────────────────────────

    public static string BrowserPath => Path.Combine(path1: AppPath, path2: "browser");

    // ── Plugins ──────────────────────────────────────────────────────────

    public static string PluginsPath => Path.Combine(path1: AppPath, path2: "plugins");
    public static string PluginConfigPath => Path.Combine(path1: PluginsPath, path2: "configurations");

    // ── Binaries (standalone NoMercy executables for auto-update) ────────

    public static string BinariesPath => Path.Combine(path1: AppPath, path2: "binaries");

    /// <summary>
    /// Path for external dependencies (FFmpeg, cloudflared, yt-dlp, etc.).
    /// For installer deployments, this is under the install directory so everything is self-contained.
    /// For standalone deployments, this falls back to BinariesPath in AppData.
    /// </summary>
    public static string DependenciesPath
    {
        get
        {
            string? installDir = Environment.GetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR");
            if (!string.IsNullOrEmpty(value: installDir))
                return Path.Combine(path1: installDir, path2: "binaries");
            return BinariesPath;
        }
    }

    public static string FfmpegFolder => Path.Combine(path1: DependenciesPath, path2: "ffmpeg");
    public static string FfmpegPath => Path.Combine(path1: FfmpegFolder, path2: "ffmpeg" + Info.ExecSuffix);
    public static string FfProbePath => Path.Combine(path1: FfmpegFolder, path2: "ffprobe" + Info.ExecSuffix);
    public static string FfPlayPath => Path.Combine(path1: FfmpegFolder, path2: "ffplay" + Info.ExecSuffix);

    // shaka-packager lives alongside ffmpeg so EncoderOptions resolves it as the
    // "packager" sibling of the ffmpeg path for CENC/raw-key DRM packaging.
    public static string ShakaPackagerPath =>
        Path.Combine(path1: FfmpegFolder, path2: "packager" + Info.ExecSuffix);

    public static string YtdlpPath => Path.Combine(path1: DependenciesPath, path2: "yt-dlp" + Info.ExecSuffix);

    public static string TesseractFolder => Path.Combine(path1: DependenciesPath, path2: "tesseract");
    public static string TesseractModelsFolder => Path.Combine(path1: TesseractFolder, path2: "tessdata");

    public static string WhisperModel { get; set; } = "ggml-large-v3";

    // The model ships inside the ffmpeg/ subfolder alongside ffmpeg.exe and the
    // libbluray jars — the build output puts everything ffmpeg-runtime there.
    public static string WhisperModelPath => Path.Combine(path1: FfmpegFolder, path2: WhisperModel + ".bin");

    public static string CloudflareDPath =>
        Path.Combine(path1: DependenciesPath, path2: "cloudflared" + Info.ExecSuffix);

    public static string ServerExePath =>
        Path.Combine(path1: BinariesPath, path2: "NoMercyMediaServer" + Info.ExecSuffix);
    public static string AppExePath => Path.Combine(path1: BinariesPath, path2: "NoMercyApp" + Info.ExecSuffix);
    public static string LauncherExePath =>
        Path.Combine(path1: BinariesPath, path2: "NoMercyLauncher" + Info.ExecSuffix);
    public static string CliExePath => Path.Combine(path1: BinariesPath, path2: "nomercy" + Info.ExecSuffix);
    public static string ServerTempExePath =>
        Path.Combine(path1: BinariesPath, path2: "NoMercyMediaServer_temp" + Info.ExecSuffix);
    public static string LauncherTempExePath =>
        Path.Combine(path1: BinariesPath, path2: "NoMercyLauncher_temp" + Info.ExecSuffix);
    public static string CliTempExePath =>
        Path.Combine(path1: BinariesPath, path2: "nomercy_temp" + Info.ExecSuffix);

    // ── Security ─────────────────────────────────────────────────────────

    public static string SecurityPath => Path.Combine(path1: AppPath, path2: "security");

    public static string CertPath => Path.Combine(path1: SecurityPath, path2: "certs");

    [Obsolete(message: "Certs are now stored encrypted in app.db. Kept for migration detection only.")]
    public static string CertFile => Path.Combine(path1: CertPath, path2: "cert.pem");

    [Obsolete(message: "Certs are now stored encrypted in app.db. Kept for migration detection only.")]
    public static string KeyFile => Path.Combine(path1: CertPath, path2: "key.pem");

    [Obsolete(message: "Certs are now stored encrypted in app.db. Kept for migration detection only.")]
    public static string CaFile => Path.Combine(path1: CertPath, path2: "ca.pem");

    public static string SecretsPath => Path.Combine(path1: SecurityPath, path2: "secrets");
    public static string SecretsStore => Path.Combine(path1: SecretsPath, path2: "secrets.bin");
    public static string SecretsKey => Path.Combine(path1: SecretsPath, path2: "secrets.key");

    // ── Misc ─────────────────────────────────────────────────────────────

    public static string AppIcon =>
        Path.Combine(path1: Directory.GetCurrentDirectory(), path2: "Assets/icon" + Info.IconSuffix);

    public static string MediaDatabase => Path.Combine(path1: DataPath, path2: "media.db");
    public static string QueueDatabase => Path.Combine(path1: DataPath, path2: "queue.db");
    public static string AppDatabase => Path.Combine(path1: DataPath, path2: "app.db");

    // ── DataProtection keys ─────────────────────────────────────────────

    public static string DataProtectionKeysDir => Path.Combine(path1: DataPath, path2: "keys");

    // ── Directory management ─────────────────────────────────────────────

    public static IEnumerable<string> AllPaths()
    {
        return
        [
            AppDataPath,
            AppPath,
            BinariesPath,
            BrowserPath,
            CachePath,
            ApiCachePath,
            EncoderCachePath,
            CertPath,
            ConfigPath,
            SeedsPath,
            DataPath,
            DataProtectionKeysDir,
            DependenciesPath,
            ImagesPath,
            LogPath,
            MusicImagesPath,
            PluginConfigPath,
            PluginsPath,
            SecretsPath,
            SecurityPath,
            TempImagesPath,
            TempPath,
            TesseractModelsFolder,
            TranscodePath,
        ];
    }

    public static Task CreateAppFolders()
    {
        if (!Directory.Exists(path: AppPath))
            Directory.CreateDirectory(path: AppPath);

        MigrateOldPaths();

        // DataProtection keys need restrictive permissions (700, not 755)
        if (!Directory.Exists(path: DataProtectionKeysDir))
        {
            Directory.CreateDirectory(path: DataProtectionKeysDir);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                DirectoryInfo keysDir = new(path: DataProtectionKeysDir)
                {
                    UnixFileMode =
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                };
            }
        }

        foreach (string path in AllPaths().Where(predicate: path => !Directory.Exists(path: path)))
        {
            Logger.Setup(message: $"Creating directory: {path}", level: LogEventLevel.Verbose);
            Directory.CreateDirectory(path: path);
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                // Set appropriate Unix permissions (755)
                DirectoryInfo dirInfo = new(path: path);
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    dirInfo.UnixFileMode =
                        UnixFileMode.UserRead
                        | UnixFileMode.UserWrite
                        | UnixFileMode.UserExecute
                        | UnixFileMode.GroupRead
                        | UnixFileMode.GroupExecute
                        | UnixFileMode.OtherRead
                        | UnixFileMode.OtherExecute;
                }
            }
        }

        // app.db should have 600 permissions (owner read/write only — contains secrets)
        if (File.Exists(path: AppDatabase) && (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
        {
            File.SetUnixFileMode(path: AppDatabase, mode: UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return Task.CompletedTask;
    }

    private static void MigrateOldPaths()
    {
        string oldRoot = Path.Combine(path1: AppPath, path2: "root");
        if (!Directory.Exists(path: oldRoot))
            return;

        Logger.Setup(message: "Migrating legacy folder structure...");

        // root/binaries → binaries
        MigrateDirectory(oldPath: Path.Combine(path1: oldRoot, path2: "binaries"), newPath: BinariesPath);

        // root/certs → security/certs
        MigrateDirectory(oldPath: Path.Combine(path1: oldRoot, path2: "certs"), newPath: CertPath);

        // root/secrets → security/secrets
        MigrateDirectory(oldPath: Path.Combine(path1: oldRoot, path2: "secrets"), newPath: SecretsPath);

        // cache/apiData → cache/api
        MigrateDirectory(oldPath: Path.Combine(path1: CachePath, path2: "apiData"), newPath: ApiCachePath);

        // config seed files → config/seeds/
        Directory.CreateDirectory(path: SeedsPath);
        MigrateFile(oldPath: Path.Combine(path1: ConfigPath, path2: "folderRootsSeed.jsonc"), newPath: FolderRootsSeedFile);
        MigrateFile(oldPath: Path.Combine(path1: ConfigPath, path2: "librariesSeed.jsonc"), newPath: LibrariesSeedFile);
        MigrateFile(oldPath: Path.Combine(path1: ConfigPath, path2: "encoderProfilesSeed.jsonc"), newPath: EncoderProfilesSeedFile);

        // Clean up empty root directory
        try
        {
            if (Directory.Exists(path: oldRoot) && !Directory.EnumerateFileSystemEntries(path: oldRoot).Any())
            {
                Directory.Delete(path: oldRoot);
                Logger.Setup(message: "Removed empty legacy root/ directory");
            }
        }
        catch
        {
            // Best-effort cleanup
        }

        Logger.Setup(message: "Migration complete");
    }

    private static void MigrateDirectory(string oldPath, string newPath)
    {
        try
        {
            if (!Directory.Exists(path: oldPath))
                return;

            if (Directory.Exists(path: newPath) && Directory.EnumerateFileSystemEntries(path: newPath).Any())
                return;

            Directory.CreateDirectory(path: Path.GetDirectoryName(path: newPath)!);
            Directory.Move(sourceDirName: oldPath, destDirName: newPath);
            Logger.Setup(message: $"Migrated {oldPath} → {newPath}");
        }
        catch (Exception ex)
        {
            Logger.Setup(message: $"Failed to migrate {oldPath}: {ex.Message}", level: LogEventLevel.Warning);
        }
    }

    private static void MigrateFile(string oldPath, string newPath)
    {
        try
        {
            if (!File.Exists(path: oldPath) || File.Exists(path: newPath))
                return;

            Directory.CreateDirectory(path: Path.GetDirectoryName(path: newPath)!);
            File.Move(sourceFileName: oldPath, destFileName: newPath);
            Logger.Setup(message: $"Migrated {oldPath} → {newPath}");
        }
        catch (Exception ex)
        {
            Logger.Setup(message: $"Failed to migrate {oldPath}: {ex.Message}", level: LogEventLevel.Warning);
        }
    }
}
