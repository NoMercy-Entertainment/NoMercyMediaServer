using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;

namespace NoMercy.Setup;

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
        return Task.Run(async () =>
        {
            Logger.Setup("Downloading Binaries");

            foreach (Download program in Downloads)
            {
                if (program.Url == null) continue;

                string destinationDirectoryName =
                    Path.Combine(AppFiles.BinariesPath, program.Path, program.Name + Info.ExecSuffix);
                bool fileExists = File.Exists(destinationDirectoryName);
                if (!fileExists)
                {
                    destinationDirectoryName = Path.Combine(AppFiles.BinariesPath, program.Path, program.Name);
                    fileExists = Directory.Exists(destinationDirectoryName);
                }

                // Parse the LastUpdated date using the correct parameter types
                if (!DateTimeOffset.TryParse(program.LastUpdated, out DateTimeOffset lastUpdatedOffset))
                {
                    Logger.Setup($"Invalid last_updated date format for {program.Name}", LogEventLevel.Warning);
                    continue;
                }

                // If file exists, compare dates to check if update is needed
                if (fileExists)
                {
                    DateTime fileDate = File.GetCreationTimeUtc(destinationDirectoryName);
                    if (fileDate >= lastUpdatedOffset.UtcDateTime) continue; // Skip if file is newer or same age
                }

                try
                {
                    await Download(program);
                    await Extract(program);
                    await Cleanup(program);

                    // Set the creation time to match the last_updated time
                    if (File.Exists(destinationDirectoryName))
                        File.SetCreationTimeUtc(destinationDirectoryName, lastUpdatedOffset.UtcDateTime);
                }
                catch (Exception e)
                {
                    Logger.Setup(e);
                    throw;
                }
            }
        });
    }

    private static async Task Download(Download program)
    {
        Logger.Setup($"Downloading {program.Name}", LogEventLevel.Verbose);

        string baseName = Path.GetFileName(program.Url?.ToString() ?? "");
        string filePath = Path.Combine(AppFiles.BinariesPath, baseName);

        HttpResponseMessage result = await HttpClient.GetAsync(program.Url);
        byte[] content = await result.Content.ReadAsByteArrayAsync();

        await File.WriteAllBytesAsync(filePath, content);
    }

    private static async Task Extract(Download program)
    {
        if (program.Url == null) return;

        string sourceArchiveFileName =
            Path.Combine(AppFiles.BinariesPath, Path.GetFileName(program.Url?.ToString() ?? ""));
        string destinationDirectoryName = Path.Combine(AppFiles.BinariesPath, program.Path, program.Name);

        Logger.Setup($"Extracting {program.Name} to {destinationDirectoryName}", LogEventLevel.Verbose);

        string extension = Path.GetExtension(program.Url!.ToString());
        if (!program.NoDelete && (string.IsNullOrEmpty(extension) || extension == ".exe"))
        {
            string file = Path.Combine(AppFiles.BinariesPath, program.Path, program.Name + Info.ExecSuffix);

            if (IsFileLocked(sourceArchiveFileName)) CloseApplicationLockingFile(sourceArchiveFileName);

            if (IsFileLocked(file)) CloseApplicationLockingFile(file);

            File.Delete(file);
            File.Move(sourceArchiveFileName, file);
            await SetExecutionPermissions(file);
            return;
        }

        try
        {
            if (!program.NoDelete && Directory.Exists(destinationDirectoryName) &&
                (sourceArchiveFileName.EndsWith(".zip") || sourceArchiveFileName.EndsWith(".tar.xz") ||
                 sourceArchiveFileName.EndsWith(".tar.gz") || sourceArchiveFileName.EndsWith("tgz")))
                Directory.Delete(destinationDirectoryName, true);

            if (sourceArchiveFileName.EndsWith(".zip"))
            {
                Directory.CreateDirectory(destinationDirectoryName);
                ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName);
                File.Delete(sourceArchiveFileName);
                await SetExecutionPermissions(destinationDirectoryName);
            }
            else if (sourceArchiveFileName.EndsWith(".tar.xz") || sourceArchiveFileName.EndsWith(".tar.gz")
                                                               || sourceArchiveFileName.EndsWith("tgz"))
            {
                Directory.CreateDirectory(destinationDirectoryName);
                await Shell.ExecAsync("tar", $"xf \"{sourceArchiveFileName}\" -C \"{destinationDirectoryName}\"");
                File.Delete(sourceArchiveFileName);
                await SetExecutionPermissions(destinationDirectoryName);
            }
        }
        catch (Exception ex)
        {
            Logger.Setup($"Failed to extract {program.Name}: {ex.Message}", LogEventLevel.Error);
            throw new($"Failed to extract {program.Name}", ex);
        }

        await Task.Delay(0);
    }

    private static async Task SetExecutionPermissions(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (File.Exists(path))
                await Shell.ExecAsync("chmod", $"+x \"{path}\"");
            else if (Directory.Exists(path)) await Shell.ExecAsync("chmod", $"-R +x \"{path}\"");

            Logger.Setup($"Set execution permissions for {path}", LogEventLevel.Verbose);
        }
    }

    private static async Task Cleanup(Download program)
    {
        if (program.Filter == "")
        {
            await Task.Delay(0);
            return;
        }

        string workingDir = Path.Combine(AppFiles.BinariesPath, program.Path, program.Name, program.Filter);
        foreach (string file in Directory.GetFiles(workingDir))
        {
            string filter = Path.DirectorySeparatorChar + program.Filter;
            string dirName = file.Replace(filter, "");

            File.Move(file, dirName);
        }

        Directory.Delete(workingDir);
    }

    private static bool IsFileLocked(string filePath)
    {
        try
        {
            using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            stream.Close();
        }
        catch (IOException)
        {
            return true;
        }

        return false;
    }

    private static void CloseApplicationLockingFile(string filePath)
    {
        Logger.Setup($"Closing application locking {filePath}", LogEventLevel.Verbose);

        foreach (Process process in Process.GetProcesses())
            try
            {
                if (process.MainModule?.FileName == null) continue;
                if (process.MainModule.FileName.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill();
                    process.WaitForExit();
                    Logger.Setup($"Closed application {process.ProcessName} locking {filePath}", LogEventLevel.Verbose);
                    break;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Ignore the error if the process is not accessible
            }
            catch (InvalidOperationException ex)
            {
                Logger.Setup($"Process {process.ProcessName} has already exited: {ex.Message}", LogEventLevel.Warning);
            }
    }
}