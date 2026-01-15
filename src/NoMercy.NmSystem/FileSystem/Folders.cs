using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.FileSystem;

public static class Folders
{
    public static void EmptyFolder(string folderPath)
    {
        DirectoryInfo directory = new(folderPath);
        if (!directory.Exists) return;

        foreach (FileInfo file in directory.GetFiles()) file.Delete();

        foreach (DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
    }

    public static long GetDirectorySize(this DirectoryInfo directoryInfo, bool recursive = true)
    {
        long startDirectorySize = 0;
        if (!directoryInfo.Exists)
            return startDirectorySize;

        try
        {
            foreach (FileInfo fileInfo in directoryInfo.GetFiles())
                Interlocked.Add(ref startDirectorySize, fileInfo.Length);

            if (recursive)
                Parallel.ForEach(directoryInfo.GetDirectories(), Config.ParallelOptions, (subDirectory) =>
                    Interlocked.Add(ref startDirectorySize, GetDirectorySize(subDirectory, recursive)));

            return startDirectorySize;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
}