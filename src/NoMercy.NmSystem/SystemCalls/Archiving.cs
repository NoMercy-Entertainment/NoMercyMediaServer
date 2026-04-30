using System.IO.Compression;
using NoMercy.NmSystem.FileSystem;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.NmSystem.SystemCalls;

public static class Archiving
{
    // Backward-compatible overload — callers in NoMercy.Setup will be migrated in the Tier-3 pass.
    public static Task<List<string>> ExtractArchive(string filePath, string destination)
    {
        return ExtractArchiveLegacy(filePath, destination);
    }

    private static async Task<List<string>> ExtractArchiveLegacy(
        string filePath,
        string destination
    )
    {
        List<string> extractedFiles;

        if (filePath.EndsWith(".zip"))
        {
            extractedFiles = ExtractZipFileLegacy(filePath, destination);
        }
        else if (
            filePath.EndsWith(".tar.xz")
            || filePath.EndsWith(".tar.gz")
            || filePath.EndsWith("tgz")
        )
        {
            extractedFiles = await ExtractTarFileLegacy(filePath, destination);
        }
        else
        {
            Logger.System($"Unsupported archive format for {filePath}", LogEventLevel.Error);
            return [];
        }

        foreach (string extractedFile in extractedFiles)
            await FilePermissions.SetExecutionPermissions(extractedFile);

        return extractedFiles;
    }

    private static List<string> ExtractZipFileLegacy(string zipFilePath, string extractToDirectory)
    {
        List<string> extractedFiles = [];
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.Combine(extractToDirectory, entry.FullName);
                string destinationDir =
                    Path.GetDirectoryName(destinationPath) ?? extractToDirectory;
                if (!Directory.Exists(destinationDir))
                    Directory.CreateDirectory(destinationDir);
                if (string.IsNullOrEmpty(entry.Name))
                    continue;
                entry.ExtractToFile(destinationPath, true);
                extractedFiles.Add(destinationPath);
            }
        }
        catch (Exception ex)
        {
            Logger.System(
                $"Failed to extract zip file {zipFilePath}: {ex.Message}",
                LogEventLevel.Error
            );
            throw new($"Failed to extract zip file {zipFilePath}", ex);
        }
        return extractedFiles;
    }

    private static async Task<List<string>> ExtractTarFileLegacy(
        string tarFilePath,
        string extractToDirectory
    )
    {
        List<string> extractedFiles = [];
        try
        {
            await Shell.ExecAsync("tar", $"xf \"{tarFilePath}\" -C \"{extractToDirectory}\"");
            Shell.ExecResult result = await Shell.ExecAsync("tar", $"tf \"{tarFilePath}\"");
            string output = result.StandardOutput;
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string destinationPath = Path.Combine(extractToDirectory, line.Trim());
                if (File.Exists(destinationPath))
                    extractedFiles.Add(destinationPath);
            }
        }
        catch (Exception ex)
        {
            Logger.System(
                $"Failed to extract tar file {tarFilePath}: {ex.Message}",
                LogEventLevel.Error
            );
            throw new($"Failed to extract tar file {tarFilePath}", ex);
        }
        return extractedFiles;
    }

    public static async Task<List<string>> ExtractArchive(
        IStorage storage,
        string filePath,
        string destination
    )
    {
        List<string> extractedFiles;

        if (filePath.EndsWith(".zip"))
        {
            extractedFiles = ExtractZipFile(storage, filePath, destination);
        }
        else if (
            filePath.EndsWith(".tar.xz")
            || filePath.EndsWith(".tar.gz")
            || filePath.EndsWith("tgz")
        )
        {
            extractedFiles = await ExtractTarFile(storage, filePath, destination);
        }
        else
        {
            Logger.System($"Unsupported archive format for {filePath}", LogEventLevel.Error);
            return [];
        }

        foreach (string extractedFile in extractedFiles)
            await FilePermissions.SetExecutionPermissions(extractedFile);

        return extractedFiles;
    }

    private static List<string> ExtractZipFile(
        IStorage storage,
        string zipFilePath,
        string extractToDirectory
    )
    {
        List<string> extractedFiles = [];

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.Combine(extractToDirectory, entry.FullName);
                string destinationDir =
                    Path.GetDirectoryName(destinationPath) ?? extractToDirectory;

                if (!storage.Exists(destinationDir))
                    storage.CreateDirectory(destinationDir);

                if (string.IsNullOrEmpty(entry.Name)) // Skip directories
                    continue;

                entry.ExtractToFile(destinationPath, true);

                extractedFiles.Add(destinationPath);
            }
        }
        catch (Exception ex)
        {
            Logger.System(
                $"Failed to extract zip file {zipFilePath}: {ex.Message}",
                LogEventLevel.Error
            );
            throw new($"Failed to extract zip file {zipFilePath}", ex);
        }

        return extractedFiles;
    }

    private static async Task<List<string>> ExtractTarFile(
        IStorage storage,
        string tarFilePath,
        string extractToDirectory
    )
    {
        List<string> extractedFiles = [];

        try
        {
            await Shell.ExecAsync("tar", $"xf \"{tarFilePath}\" -C \"{extractToDirectory}\"");

            Shell.ExecResult result = await Shell.ExecAsync("tar", $"tf \"{tarFilePath}\"");
            string output = result.StandardOutput;

            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string destinationPath = Path.Combine(extractToDirectory, line.Trim());
                if (storage.Exists(destinationPath))
                    extractedFiles.Add(destinationPath);
            }
        }
        catch (Exception ex)
        {
            Logger.System(
                $"Failed to extract tar file {tarFilePath}: {ex.Message}",
                LogEventLevel.Error
            );
            throw new($"Failed to extract tar file {tarFilePath}", ex);
        }

        return extractedFiles;
    }
}
