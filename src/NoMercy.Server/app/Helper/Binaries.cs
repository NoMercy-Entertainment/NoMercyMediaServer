using System.IO.Compression;
using System.Runtime.InteropServices;
using NoMercy.Networking;
using NoMercy.NmSystem;
using Serilog.Events;

namespace NoMercy.Server.app.Helper;

public static class Binaries
{
    private static List<Download> Downloads { get; set; }

    private static readonly HttpClient Client = new();

    static Binaries()
    {
        Client.DefaultRequestHeaders.Add("User-Agent", ApiInfo.UserAgent);

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

        HttpResponseMessage result = await Client.GetAsync(program.Url);
        byte[] content = await result.Content.ReadAsByteArrayAsync();

        string baseName = Path.GetFileName(program.Url?.ToString() ?? "");

        await File.WriteAllBytesAsync(Path.Combine(AppFiles.BinariesPath, baseName), content);
    }

    private static async Task Extract(Download program)
    {
        string sourceArchiveFileName =
            Path.Combine(AppFiles.BinariesPath, Path.GetFileName(program.Url?.ToString() ?? ""));
        string destinationDirectoryName = Path.Combine(AppFiles.BinariesPath, program.Name);

        Logger.Setup($"Extracting {program.Name} to {destinationDirectoryName}");

        if (Directory.Exists(destinationDirectoryName))
            Directory.Delete(destinationDirectoryName, true);

        Directory.CreateDirectory(destinationDirectoryName);

        try
        {
            if (sourceArchiveFileName.EndsWith(".zip"))
                ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName);
            else if (sourceArchiveFileName.EndsWith(".tar.xz") || sourceArchiveFileName.EndsWith(".tar.gz"))
                await Shell.Exec("tar", $"xf \"{sourceArchiveFileName}\" -C \"{destinationDirectoryName}\"");
            else
                throw new NotSupportedException("Unsupported archive format");

            File.Delete(sourceArchiveFileName);
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to extract {program.Name}: {ex.Message}", LogEventLevel.Error);
            throw new Exception($"Failed to extract {program.Name}", ex);
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