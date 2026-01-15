using System.IO.Compression;
using NoMercy.NmSystem.FileSystem;
using Serilog.Events;

namespace NoMercy.NmSystem.SystemCalls;

public static class Archiving
{
    public static async Task<List<string>> ExtractArchive(string filePath, string destination)
    {
        List<string> extractedFiles;

        if (filePath.EndsWith(".zip"))
        {
            extractedFiles = ExtractZipFile(filePath, destination);
        }
        else if (filePath.EndsWith(".tar.xz") || filePath.EndsWith(".tar.gz") || filePath.EndsWith("tgz"))
        {
            extractedFiles = await ExtractTarFile(filePath, destination);
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
    
    private static List<string> ExtractZipFile(string zipFilePath, string extractToDirectory)
    {
        List<string> extractedFiles = [];

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.Combine(extractToDirectory, entry.FullName);
                string destinationDir = Path.GetDirectoryName(destinationPath) ?? extractToDirectory;

                if (!Directory.Exists(destinationDir))
                    Directory.CreateDirectory(destinationDir);

                if (string.IsNullOrEmpty(entry.Name)) // Skip directories
                    continue;

                entry.ExtractToFile(destinationPath, true);
                
                extractedFiles.Add(destinationPath);
            }
        }
        catch (Exception ex)
        {
            Logger.System($"Failed to extract zip file {zipFilePath}: {ex.Message}", LogEventLevel.Error);
            throw new($"Failed to extract zip file {zipFilePath}", ex);
        }

        return extractedFiles;
    }

    private static async Task<List<string>> ExtractTarFile(string tarFilePath, string extractToDirectory)
    {
        List<string> extractedFiles = [];

        try
        {
            await Shell.ExecAsync("tar", $"xf \"{tarFilePath}\" -C \"{extractToDirectory}\"");
            
            Shell.ExecResult result = await Shell.ExecAsync("tar" , $"tf \"{tarFilePath}\"");
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
            Logger.System($"Failed to extract tar file {tarFilePath}: {ex.Message}", LogEventLevel.Error);
            throw new($"Failed to extract tar file {tarFilePath}", ex);
        }

        return extractedFiles;
    }
}