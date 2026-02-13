using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Providers.Helpers;

public static class CacheController
{
    private const long MaxCacheSizeBytes = 500_000_000; // 500MB
    private const int MaxLockEntries = 10_000;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new();

    private static SemaphoreSlim GetLock(string path)
    {
        if (FileLocks.Count > MaxLockEntries)
        {
            PruneLocks();
        }

        return FileLocks.GetOrAdd(path, _ => new(1, 1));
    }

    private static void PruneLocks()
    {
        foreach (KeyValuePair<string, SemaphoreSlim> entry in FileLocks)
        {
            if (entry.Value.CurrentCount == 1)
            {
                if (FileLocks.TryRemove(entry.Key, out SemaphoreSlim? removed))
                {
                    removed.Dispose();
                }
            }
        }
    }

    public static string GenerateFileName(string url)
    {
        return CreateMd5(url);
    }

    private static string CreateMd5(string input)
    {
        byte[] inputBytes = Encoding.ASCII.GetBytes(input);
        byte[] hashBytes = MD5.HashData(inputBytes);

        return Convert.ToHexString(hashBytes);
    }

    public static bool Read<T>(string url, out T? value, bool xml = false) where T : class?
    {
        if (Config.IsDev == false)
        {
            value = default;
            return false;
        }

        string fullname = Path.Combine(AppFiles.ApiCachePath, GenerateFileName(url));
        SemaphoreSlim fileLock = GetLock(fullname);
        fileLock.Wait();

        try
        {
            if (File.Exists(fullname) == false)
            {
                value = default;
                return false;
            }

            // invalidate cache after 1 day of creation date
            if (File.GetCreationTime(fullname) < DateTime.Now.SubDays(1))
            {
                File.Delete(fullname);
                value = default;
                return false;
            }

            T? data;
            try
            {
                string d = File.ReadAllText(fullname);
                data = xml ? d.FromXml<T>() : d.FromJson<T>();
            }
            catch (Exception)
            {
                value = default;
                return false;
            }

            if (data == null)
            {
                value = default;
                return true;
            }

            if (data is { } item)
            {
                value = item;
                return true;
            }

            value = default;
            return false;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public static async Task Write(string url, string data)
    {
        if (Config.IsDev == false) return;

        string fullname = Path.Combine(AppFiles.ApiCachePath, GenerateFileName(url));
        SemaphoreSlim fileLock = GetLock(fullname);

        for (int retry = 0; retry <= 10; retry++)
        {
            await fileLock.WaitAsync();

            try
            {
                await File.WriteAllTextAsync(fullname, data);
                PruneCache();
                return;
            }
            catch (Exception) when (retry < 10)
            {
            }
            finally
            {
                fileLock.Release();
            }

            await Task.Delay(50 * (retry + 1));
        }

        Logger.App($"CacheController: Failed to write {fullname}");
    }

    internal static void PruneCache()
    {
        PruneCache(AppFiles.ApiCachePath, MaxCacheSizeBytes);
    }

    internal static void PruneCache(string cachePath, long maxSizeBytes)
    {
        DirectoryInfo cacheDir = new(cachePath);
        if (cacheDir.Exists == false) return;

        FileInfo[] files = cacheDir.GetFiles()
            .OrderBy(f => f.CreationTime)
            .ToArray();

        long totalSize = files.Sum(f => f.Length);

        foreach (FileInfo file in files)
        {
            if (totalSize <= maxSizeBytes) break;

            try
            {
                totalSize -= file.Length;
                file.Delete();
            }
            catch (Exception)
            {
                // File may be locked by another operation
            }
        }
    }
}
