using System.IO.Compression;
using System.Runtime.InteropServices;
using NoMercy.Networking;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Server.app.Helper;

public static class Binaries
{

    private static List<Download> Downloads { get; set; }
    private static readonly HttpClient HttpClient = new();

    static Binaries()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Downloads = ApiInfo.BinaryList.Linux;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Downloads = ApiInfo.BinaryList.Mac;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Downloads = ApiInfo.BinaryList.Windows;
        else
            Downloads = [];
    }

    public static Task DownloadAll()
    {
        Task.Run(async () =>
        {
            Logger.Setup("Downloading Binaries");
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (Download program in Downloads)
            {
                if (program.Url == null) continue;
                
                string destinationDirectoryName = Path.Combine(AppFiles.BinariesPath, program.Name);
                DateTime creationTime = Directory.GetCreationTimeUtc(destinationDirectoryName);

                int days = creationTime.Subtract(program.LastUpdated).Days;

                if (Directory.Exists(destinationDirectoryName) && days > 0) continue;

                await Download(program);
                await Extract(program);
                await Cleanup(program);
            }
        }).Wait();

        return Task.CompletedTask;
    }

    private static async Task Download(Download program)
    {
        Logger.Setup($"Downloading {program.Name}");

        HttpResponseMessage result = await HttpClient.GetAsync(program.Url);
        byte[] content = await result.Content.ReadAsByteArrayAsync();

        string baseName = Path.GetFileName(program.Url?.ToString() ?? "");

        await File.WriteAllBytesAsync(Path.Combine(AppFiles.BinariesPath, baseName), content);
    }

    private static async Task Extract(Download program)
    {
        if (program.Url == null) return;
        
        string sourceArchiveFileName =
            Path.Combine(AppFiles.BinariesPath, Path.GetFileName(program.Url?.ToString() ?? ""));
        string destinationDirectoryName = Path.Combine(AppFiles.BinariesPath, program.Name);
        
        Logger.Setup($"Extracting {program.Name} to {destinationDirectoryName}");
        
        string extension = Path.GetExtension(program.Url!.ToString());
        if (string.IsNullOrEmpty(extension) || extension == ".exe")
        {
            File.Delete(Path.Combine(AppFiles.BinariesPath, program.Name + Info.ExecSuffix));
            File.Move(sourceArchiveFileName, Path.Combine(AppFiles.BinariesPath, program.Name + Info.ExecSuffix));
            return;
        }
        
        try
        {
            if (Directory.Exists(destinationDirectoryName) && 
                (sourceArchiveFileName.EndsWith(".zip") || sourceArchiveFileName.EndsWith(".tar.xz") ||
                                                           sourceArchiveFileName.EndsWith(".tar.gz")))
            {
                Directory.Delete(destinationDirectoryName, true);
            }

            if (sourceArchiveFileName.EndsWith(".zip"))
            {
                Directory.CreateDirectory(destinationDirectoryName);
                ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName);
                File.Delete(sourceArchiveFileName);
            }
            else if (sourceArchiveFileName.EndsWith(".tar.xz") || sourceArchiveFileName.EndsWith(".tar.gz"))
            {
                Directory.CreateDirectory(destinationDirectoryName);
                await Shell.Exec("tar", $"xf \"{sourceArchiveFileName}\" -C \"{destinationDirectoryName}\"");
                File.Delete(sourceArchiveFileName);
            }
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to extract {program.Name}: {ex.Message}", LogEventLevel.Error);
            throw new($"Failed to extract {program.Name}", ex);
        }

        await Task.Delay(0);
    }

    private static async Task Cleanup(Download program)
    {
        if (program.Filter == "")
        {
            await Task.Delay(0);
            return;
        }

        string workingDir = Path.Combine(AppFiles.BinariesPath, program.Name, program.Filter);
        foreach (string file in Directory.GetFiles(workingDir))
        {
            string filter = Path.DirectorySeparatorChar + program.Filter;
            string dirName = file.Replace(filter, "");

            File.Move(file, dirName);
        }

        Directory.Delete(workingDir);
    }
}