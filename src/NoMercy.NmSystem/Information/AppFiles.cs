// ReSharper disable MemberCanBePrivate.Global

using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.NmSystem.Information;

public static class AppFiles
{
    public static readonly string ApplicationName = "NoMercy MediaServer";

    public static readonly string AppDataPath = Environment.OSVersion.Platform == PlatformID.Unix
        ? Path.Combine(
            Environment.GetEnvironmentVariable("HOME") ?? "/home/current",
            ".local/share")
        : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string AppPath => Config.IsDev
        ? Path.Combine(AppDataPath, "NoMercy_dev")
        : Path.Combine(AppDataPath, "NoMercy");

    public static string ConfigPath => Path.Combine(AppPath, "config");
    public static string TokenFile => Path.Combine(ConfigPath, "token.json");
    public static string AuthKeysFile => Path.Combine(ConfigPath, "auth_keys.json");
    public static string JwksCacheFile => Path.Combine(ConfigPath, "jwks_cache.json");
    public static string FolderRootsSeedFile => Path.Combine(ConfigPath, "folderRootsSeed.jsonc");
    public static string LibrariesSeedFile => Path.Combine(ConfigPath, "librariesSeed.jsonc");
    public static string EncoderProfilesSeedFile => Path.Combine(ConfigPath, "encoderProfilesSeed.jsonc");

    public static string DataPath => Path.Combine(AppPath, "data");
    public static string LogPath => Path.Combine(AppPath, "log");

    public static string CachePath => Path.Combine(AppPath, "cache");
    public static string ApiCachePath => Path.Combine(CachePath, "apiData");
    public static string TempPath => Path.Combine(CachePath, "temp");
    public static string TranscodePath => Path.Combine(CachePath, "transcode");
    public static string ImagesPath => Path.Combine(CachePath, "images");
    public static string MusicImagesPath => Path.Combine(ImagesPath, "music");
    public static string TempImagesPath => Path.Combine(ImagesPath, "temp");

    public static string PluginsPath => Path.Combine(AppPath, "plugins");
    public static string PluginConfigPath => Path.Combine(PluginsPath, "configurations");

    public static string RootPath => Path.Combine(AppPath, "root");
    public static string BinariesPath => Path.Combine(RootPath, "binaries");
    public static string FfmpegFolder => Path.Combine(BinariesPath, "ffmpeg");
    public static string FfmpegPath => Path.Combine(FfmpegFolder, "ffmpeg" + Info.ExecSuffix);
    public static string FfProbePath => Path.Combine(FfmpegFolder, "ffprobe" + Info.ExecSuffix);
    public static string FfPlayPath => Path.Combine(FfmpegFolder, "ffplay" + Info.ExecSuffix);
    
    public static string YtdlpPath => Path.Combine(BinariesPath, "yt-dlp" + Info.ExecSuffix);

    public static string TesseractFolder => Path.Combine(BinariesPath, "tesseract");
    public static string TesseractModelsFolder => Path.Combine(TesseractFolder, "tessdata");
    
    public static string WhisperModel { get; set; } = "ggml-large-v3";
    public static string WhisperModelPath => Path.Combine(BinariesPath, WhisperModel + ".bin");

    public static string CloudflareDPath => Path.Combine(BinariesPath, "cloudflared" + Info.ExecSuffix);

    public static string ServerExePath => Path.Combine(BinariesPath, "NoMercyMediaServer" + Info.ExecSuffix);
    public static string AppExePath => Path.Combine(BinariesPath, "NoMercyApp" + Info.ExecSuffix);
    public static string ServerTempExePath => Path.Combine(BinariesPath, "NoMercyMediaServer_temp" + Info.ExecSuffix);

    public static string CertPath => Path.Combine(RootPath, "certs");
    public static string CertFile => Path.Combine(CertPath, "cert.pem");
    public static string KeyFile => Path.Combine(CertPath, "key.pem");
    public static string CaFile => Path.Combine(CertPath, "ca.pem");

    public static string SecretsPath => Path.Combine(RootPath, "secrets");
    public static string SecretsStore => Path.Combine(SecretsPath, "secrets.bin");
    public static string SecretsKey => Path.Combine(SecretsPath, "secrets.key");

    public static string AppIcon =>
        Path.Combine(Directory.GetCurrentDirectory(), "Assets/icon" + Info.IconSuffix);

    public static string MediaDatabase => Path.Combine(DataPath, "media.db");
    public static string QueueDatabase => Path.Combine(DataPath, "queue.db");

    public static IEnumerable<string> AllPaths()
    {
        return
        [
            ApiCachePath,
            AppDataPath,
            AppPath,
            BinariesPath,
            CachePath,
            CertPath,
            ConfigPath,
            DataPath,
            ImagesPath,
            LogPath,
            MusicImagesPath,
            PluginConfigPath,
            PluginsPath,
            RootPath,
            SecretsPath,
            TempImagesPath,
            TempPath,
            TesseractModelsFolder,
            TranscodePath
        ];
    }

    public static Task CreateAppFolders()
    {
        if (!Directory.Exists(AppPath))
            Directory.CreateDirectory(AppPath);

        foreach (string path in AllPaths().Where(path => !Directory.Exists(path)))
        {
            Logger.Setup($"Creating directory: {path}");
            Directory.CreateDirectory(path);
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                // Set appropriate Unix permissions (755)
                DirectoryInfo dirInfo = new(path);
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    dirInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                           UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                           UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                }
            }
        }

        return Task.CompletedTask;
    }
}