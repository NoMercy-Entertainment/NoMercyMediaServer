using System.Globalization;
using System.Runtime.InteropServices;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.FileSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;
using Downloader = NoMercy.NmSystem.SystemCalls.Download;
using FileAttributes = NoMercy.NmSystem.FileSystem.FileAttributes;
using HttpClient = System.Net.Http.HttpClient;

namespace NoMercy.Setup;

public static class Binaries
{
    private static readonly HttpClient HttpClient = new();
    
    private const string GithubMediaServerApiUrl    = "https://api.github.com/repos/NoMercy-Entertainment/NoMercyMediaServer/releases/latest";
    private const string GithubFfmpegApiUrl         = "https://api.github.com/repos/NoMercy-Entertainment/NoMercyFFMpeg/releases/latest";
    private const string GithubTesseractApiUrl      = "https://api.github.com/repos/NoMercy-Entertainment/NoMercyTesseract/releases/latest";
    private const string GithubWhisperModelApiUrl   = "https://api.github.com/repos/NoMercy-Entertainment/WhisperGmmlModels/releases/latest";
    
    private const string GithubYtdlpApiUrl          = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
    private const string GithubCloudflaredApiUrl    = "https://api.github.com/repos/cloudflare/cloudflared/releases/latest";

    static Binaries()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
    }

    public static Task DownloadAll()
    {
        return Task.Run(async () =>
        {
            Logger.Setup("Downloading Binaries");

            await DownloadApp();
            await DownloadFfmpeg();
            await DownloadCloudflared();
            await DownloadYtdlp();
            await DownloadWhisperModels(AppFiles.WhisperModel);
            
            string currentCulture = CultureInfo.CurrentCulture.EnglishLanguageTag();
            await DownloadTesseractData(["eng", "jpn", currentCulture]);
        });
    }

    private static bool CheckLocalVersion(GithubReleaseResponse releaseInfo, string destination, out string version)
    {
        version = releaseInfo.TagName.StartsWith("v")
            ? releaseInfo.TagName[1..]
            : releaseInfo.TagName;
        
        bool fileExists = File.Exists(destination);
        if (!fileExists) return false;
        
        DateTime creationTime = File.GetCreationTimeUtc(destination);
        DateTimeOffset releaseDate = releaseInfo.PublishedAt != DateTimeOffset.MinValue
            ? releaseInfo.PublishedAt.UtcDateTime
            : DateTimeOffset.Now;
        
        return creationTime >= releaseDate;
    }
    
    private static async Task<GithubReleaseResponse> GetLatestReleaseInfo(string apiUrl)
    {
        try
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(apiUrl);

            response.EnsureSuccessStatusCode();

            string jsonResponse = await response.Content.ReadAsStringAsync();

            return jsonResponse.FromJson<GithubReleaseResponse>() ?? new GithubReleaseResponse();
        } catch (Exception e)
        {
            Logger.Setup($"Error fetching release info from {apiUrl}: {e.Message}", LogEventLevel.Error);
            return new();
        }
    }
    
    private static async Task DownloadApp()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubMediaServerApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for App release.", LogEventLevel.Error);
            return;
        }

        if (CheckLocalVersion(releaseInfo, AppFiles.AppExePath, out string version))
        {
            Logger.Setup($"App is already up to date (version {version})", LogEventLevel.Verbose);
            return;
        }
        
        await Downloader.DeleteSourceDownload(AppFiles.AppExePath);

        Uri? downloadUrl = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Equals("NoMercyApp-windows-x64.exe", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Equals("NoMercyApp-linux-arm64", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Equals("NoMercyApp-linux-x64", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Equals("NoMercyApp-macos-x64.dmg", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
        }

        if (downloadUrl == null)
        {
            Logger.Setup("No suitable NoMercyApp asset found for the current platform.", LogEventLevel.Error);
            return;
        }
        
        string path = await Downloader.DownloadFile("NoMercyApp", downloadUrl, AppFiles.AppExePath);
        
        await FileAttributes.SetCreatedAttribute(path, releaseInfo.PublishedAt);
        
        await FilePermissions.SetExecutionPermissions(path);
    }

    private static async Task DownloadFfmpeg()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubFfmpegApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for FFMpeg release.", LogEventLevel.Error);
            return;
        }

        if (CheckLocalVersion(releaseInfo, AppFiles.FfmpegPath, out string version))
        {
            Logger.Setup($"Ffmpeg is already up to date (version {version})", LogEventLevel.Verbose);
            return;
        }
        
        await Downloader.DeleteSourceDownload(AppFiles.FfmpegPath);
        await Downloader.DeleteSourceDownload(AppFiles.FfProbePath);
        await Downloader.DeleteSourceDownload(AppFiles.FfPlayPath);
        
        Uri? downloadUrl = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Contains("windows"))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Contains("linux") && a.Name.Contains("aarch64"))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Contains("linux") && a.Name.Contains("x86_64"))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Contains("darwin") && a.Name.Contains("arm64"))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Contains("darwin") && a.Name.Contains("x86_64"))?.BrowserDownloadUrl;
        }

        if (downloadUrl == null)
        {
            Logger.Setup("No suitable FFMpeg asset found for the current platform.", LogEventLevel.Error);
            return;
        }
        
        string path = await Downloader.DownloadFile("FFMpeg", downloadUrl);
        
        List<string> files = await Archiving.ExtractArchive(path, AppFiles.FfmpegFolder);
        foreach (string file in files)
        {
            await FileAttributes.SetCreatedAttribute(file, releaseInfo.PublishedAt);
        }
        
        await Downloader.DeleteSourceDownload(path);
        
    }
    
    private static async Task DownloadYtdlp()
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubYtdlpApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for yt-dlp release.", LogEventLevel.Error);
            return;
        }
        
        if (CheckLocalVersion(releaseInfo, AppFiles.YtdlpPath, out string version))
        {
            Logger.Setup($"Yt-dlp is already up to date (version {version})", LogEventLevel.Verbose);
            return;
        }
        
        await Downloader.DeleteSourceDownload(AppFiles.YtdlpPath);

        Uri? downloadUrl = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Equals("yt-dlp_x86.exe", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Equals("yt-dlp_linux_aarch64", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Equals("yt-dlp_linux", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Equals("yt-dlp_macos", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;
        }

        if (downloadUrl == null)
        {
            Logger.Setup("No suitable yt-dlp asset found for the current platform.", LogEventLevel.Error);
            return;
        }
        
        string outputPath = await Downloader.DownloadFile("yt-dlp", downloadUrl, AppFiles.YtdlpPath);
        
        await FileAttributes.SetCreatedAttribute(outputPath, releaseInfo.PublishedAt);
        
        Logger.Setup($"Downloaded yt-dlp to {outputPath}");
    }

    private static async Task DownloadCloudflared()
    {
        string destinationPath = AppFiles.CloudflareDPath;
        
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubCloudflaredApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for cloudflared release.", LogEventLevel.Error);
            return;
        }
        
        if (CheckLocalVersion(releaseInfo, destinationPath, out string version))
        {
            Logger.Setup($"Cloudflared is already up to date (version {version})", LogEventLevel.Verbose);
            return;
        }
        
        await Downloader.DeleteSourceDownload(destinationPath);

        Uri? downloadUrl = null;
        bool needsExtraction = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = releaseInfo.Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-windows-amd64.exe"))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            downloadUrl = releaseInfo.Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-linux-arm"))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            downloadUrl = releaseInfo.Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-linux-amd64"))?.BrowserDownloadUrl;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            downloadUrl = releaseInfo.Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-darwin-arm64.tgz"))?.BrowserDownloadUrl;
            needsExtraction = true;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            downloadUrl = releaseInfo.Assets.FirstOrDefault(a => a.Name.Equals("cloudflared-darwin-amd64.tgz"))?.BrowserDownloadUrl;
            needsExtraction = true;
        }

        if (downloadUrl == null)
        {
            Logger.Setup("No suitable cloudflared asset found for the current platform.", LogEventLevel.Error);
            return;
        }
        
        string path = await Downloader.DownloadFile("cloudflared", downloadUrl);
        
        Logger.Setup($"Downloaded cloudflared to {path}");
        
        if (needsExtraction)
        {
            List<string> files = await Archiving.ExtractArchive(path, AppFiles.BinariesPath);
            foreach (string file in files)
            {
                await FileAttributes.SetCreatedAttribute(file, releaseInfo.PublishedAt);
            }
            await Downloader.DeleteSourceDownload(path);
        }
        else
        {
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            
            File.Move(path, destinationPath);
            
            await FileAttributes.SetCreatedAttribute(destinationPath, releaseInfo.PublishedAt);
            
            await FilePermissions.SetExecutionPermissions(destinationPath);
        }
        
    }

    private static async Task DownloadWhisperModels(string modelName = "ggml-large-v3")
    {
        string destinationPath = Path.Combine(AppFiles.FfmpegFolder, modelName + ".bin");

        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubWhisperModelApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for WhisperGmmlModels release.", LogEventLevel.Error);
            return;
        }
        
        if (CheckLocalVersion(releaseInfo, destinationPath, out string version))
        {
            Logger.Setup($"Whisper LLM model is already up to date (version {version})", LogEventLevel.Verbose);
            return;
        }
        
        await Downloader.DeleteSourceDownload(destinationPath);
        
        List<Uri> downloadUrls = releaseInfo.Assets
            .Where(a => a.Name.Contains(modelName, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.BrowserDownloadUrl)
            .ToList();

        if (downloadUrls.Count == 0)
        {
            Logger.Setup($"No assets found for model {modelName} in WhisperGmmlModels release.", LogEventLevel.Error);
            return;
        }

        List<string> paths = [];
        foreach (Uri downloadUrl in downloadUrls)
        {
            paths.Add(await Downloader.DownloadFile("WhisperGmmlModels", downloadUrl));
        }

        if (downloadUrls.Count > 1)
        {
            string outputPath = await ConcatenateModelParts(modelName, downloadUrls);
            
            foreach (string path in paths)
            {
                await Downloader.DeleteSourceDownload(path);
            }
            
            await FileAttributes.SetCreatedAttribute(outputPath, releaseInfo.PublishedAt);
            
            Logger.Setup($"Downloaded and concatenated Whisper model parts to {outputPath}");
        }
        else
        {
            Logger.Setup($"Downloaded Whisper model to {paths[0]}");
        }
    }
    
    private static async Task<string> ConcatenateModelParts(string modelName, IEnumerable<Uri> partUrls)
    {
        string destinationPath = Path.Combine(AppFiles.FfmpegFolder, modelName + ".bin");

        await using FileStream destinationStream = new(destinationPath, FileMode.OpenOrCreate, FileAccess.Write);
    
        foreach (Uri partUrl in partUrls)
        {
            string partPath = Path.Combine(AppFiles.BinariesPath, Path.GetFileName(partUrl.ToString()));

            await using FileStream partStream = new(partPath, FileMode.Open, FileAccess.Read);
            await partStream.CopyToAsync(destinationStream);
        }
        
        Logger.Setup($"Concatenated model parts into {destinationPath}", LogEventLevel.Verbose);

        return destinationPath;
    }
    
    private static async Task DownloadTesseractData(IEnumerable<string> languages)
    {
        GithubReleaseResponse releaseInfo = await GetLatestReleaseInfo(GithubTesseractApiUrl);
        if (releaseInfo.Assets.Length == 0)
        {
            Logger.Setup("No assets found for TesseractData release.", LogEventLevel.Error);
            return;
        }

        foreach (string lang in languages)
        {
            Uri? downloadUrl = releaseInfo.Assets
                .FirstOrDefault(a => a.Name.Equals($"{lang}.traineddata", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl;

            if (downloadUrl == null)
            {
                Logger.Setup($"No asset found for language {lang} in TesseractData release.", LogEventLevel.Warning);
                continue;
            }
            
            string destinationPath = Path.Combine(AppFiles.TesseractModelsFolder, $"{lang}.traineddata");
            
            if (CheckLocalVersion(releaseInfo, destinationPath, out string version))
            {
                Logger.Setup($"Tesseract data for {lang} is already up to date (version {version})", LogEventLevel.Verbose);
                continue;
            }
            
            await Downloader.DeleteSourceDownload(destinationPath);
        
            string path = await Downloader.DownloadFile($"Tesseract data for {lang}", downloadUrl, $"{lang}.traineddata");
            
            File.Move(path, destinationPath);
            
            await FileAttributes.SetCreatedAttribute(destinationPath, releaseInfo.PublishedAt);
            
            Logger.Setup($"Downloaded Tesseract data for {lang} to {destinationPath}");
        }
    }
}