// ReSharper disable MemberCanBePrivate.Global

namespace NoMercy.NmSystem;

public static class AppFiles
{
    public static readonly string AppDataPath =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static readonly string AppPath = Path.Combine(AppDataPath, "NoMercy_C#");

    public static readonly string CachePath = Path.Combine(AppPath, "cache");
    public static readonly string ConfigPath = Path.Combine(AppPath, "config");
    public static readonly string TokenFile = Path.Combine(ConfigPath, "token.json");
    public static readonly string ConfigFile = Path.Combine(ConfigPath, "config.json");
    public static readonly string FolderRootsSeedFile = Path.Combine(ConfigPath, "folderRootsSeed.jsonc");
    public static readonly string LibrariesSeedFile = Path.Combine(ConfigPath, "librariesSeed.jsonc");
    public static readonly string EncoderProfilesSeedFile = Path.Combine(ConfigPath, "encoderProfilesSeed.jsonc");

    public static readonly string DataPath = Path.Combine(AppPath, "data");

    public static readonly string LogPath = Path.Combine(AppPath, "log");
    public static readonly string MetadataPath = Path.Combine(AppPath, "metadata");
    public static readonly string PluginsPath = Path.Combine(AppPath, "plugins");
    public static readonly string RootPath = Path.Combine(AppPath, "root");

    public static readonly string ApiCachePath = Path.Combine(CachePath, "apiData");
    public static readonly string TempPath = Path.Combine(CachePath, "temp");
    public static readonly string TranscodePath = Path.Combine(CachePath, "transcode");
    public static readonly string ImagesPath = Path.Combine(CachePath, "images");
    public static readonly string MusicImagesPath = Path.Combine(ImagesPath, "music");
    public static readonly string TempImagesPath = Path.Combine(ImagesPath, "temp");

    public static readonly string PluginConfigPath = Path.Combine(PluginsPath, "configurations");
    public static readonly string UserDataPath = Path.Combine(DataPath, "userData");

    public static readonly string BinariesPath = Path.Combine(RootPath, "binaries");
    public static readonly string FfmpegFolder = Path.Combine(RootPath, "binaries", "ffmpeg");
    public static readonly string FfmpegPath = Path.Combine(FfmpegFolder, "ffmpeg" + Info.ExecSuffix);
    public static readonly string FfProbePath = Path.Combine(FfmpegFolder, "ffprobe" + Info.ExecSuffix);
    public static readonly string FpCalcPath = Path.Combine(BinariesPath, "fpcalc", "fpcalc" + Info.ExecSuffix);
    public static string UpdaterExePath => Path.Combine(BinariesPath, "NoMercyUpdater" + Info.ExecSuffix);
    public static string ServerExePath => Path.Combine(BinariesPath, "NoMercyMediaServer" + Info.ExecSuffix);
    public static string ServerTempExePath => Path.Combine(BinariesPath, "NoMercyMediaServer_temp" + Info.ExecSuffix);
    
    public static readonly string SubtitleEdit =
        Path.Combine(BinariesPath, "subtitleedit", "SubtitleEdit" + Info.ExecSuffix);

    public static readonly string CertPath = Path.Combine(RootPath, "certs");
    public static readonly string CertFile = Path.Combine(CertPath, "cert.pem");
    public static readonly string KeyFile = Path.Combine(CertPath, "key.pem");
    public static readonly string CaFile = Path.Combine(CertPath, "ca.pem");

    public static readonly string SecretsPath = Path.Combine(RootPath, "secrets");
    public static readonly string SecretsStore = Path.Combine(SecretsPath, "secrets.bin");
    public static readonly string SecretsKey = Path.Combine(SecretsPath, "secrets.key");

    public static readonly string AppIcon = Path.Combine(Directory.GetCurrentDirectory(), "Assets/icon.ico");

    public static readonly string MediaDatabase = Path.Combine(DataPath, "media.db");
    public static readonly string QueueDatabase = Path.Combine(DataPath, "queue.db");

    public static IEnumerable<string> AllPaths()
    {
        return
        [
            AppDataPath,
            AppPath,
            CachePath,
            ConfigPath,
            DataPath,
            LogPath,
            MetadataPath,
            PluginsPath,
            RootPath,
            ApiCachePath,
            ImagesPath,
            TempPath,
            TranscodePath,
            PluginConfigPath,
            UserDataPath,
            BinariesPath,
            CertPath,
            SecretsPath,
            MusicImagesPath,
            TempImagesPath
        ];
    }

    public static Task CreateAppFolders()
    {
        if (!Directory.Exists(AppPath))
            Directory.CreateDirectory(AppPath);

        foreach (string path in AllPaths().Where(path => !Directory.Exists(path)))
            // Logger.Setup($"Creating directory: {path}");
            Directory.CreateDirectory(path);

        return Task.CompletedTask;
    }
}