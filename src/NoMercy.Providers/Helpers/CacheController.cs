// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;

namespace NoMercy.Providers.Helpers;

public static class CacheController
{
    private const long MaxCacheSizeBytes = 500_000_000; // 500MB
    private const int MaxLockEntries = 10_000;

    // Prune is O(N) over the entire cache directory (List + sort + sum). Heavy
    // parallel TMDB fetches (cast/crew with 50-200 people per show) used to
    // call Prune on every Write, scanning thousands of JSON files thousands of
    // times under the file-lock-held section. Throttle to at most once a
    // minute and fire-and-forget so writes return immediately.
    private static readonly TimeSpan _pruneInterval = TimeSpan.FromMinutes(1);
    private static long _lastPruneTicksUtc = DateTime.MinValue.Ticks;
    private static int _pruneInFlight;

    private static IStorage? _storage;

    public static void Initialize(IStorage storage)
    {
        _storage = storage;
    }

    private static IStorage Storage =>
        _storage
        ?? throw new InvalidOperationException(
            "CacheController has not been initialized. Call CacheController.Initialize() at startup."
        );

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

    public static async Task<(bool Found, T? Value)> ReadAsync<T>(string url, bool xml = false)
        where T : class?
    {
        // No cache configured (e.g. unit tests that never call Initialize) —
        // behave as a cache miss rather than throwing.
        if (_storage is null)
            return (false, default);

        string fullname = Path.Combine(AppFiles.ApiCachePath, GenerateFileName(url));
        SemaphoreSlim fileLock = GetLock(fullname);
        await fileLock.WaitAsync();

        try
        {
            IStorage storage = Storage;

            if (!storage.Exists(fullname))
            {
                return (false, default);
            }

            // invalidate cache after 1 day of last write time
            if (storage.LastModified(fullname) < DateTimeOffset.UtcNow.AddDays(-1))
            {
                storage.Delete(fullname);
                return (false, default);
            }

            T? data;
            try
            {
                string d = Encoding.UTF8.GetString(storage.Read(fullname));
                data = xml ? d.FromXml<T>() : d.FromJson<T>();
            }
            catch (Exception)
            {
                return (false, default);
            }

            if (data == null)
            {
                return (true, default);
            }

            if (data is { } item)
            {
                return (true, item);
            }

            return (false, default);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public static async Task Write(string url, string data)
    {
        // No cache configured (uninitialized) — skip silently instead of throwing.
        if (_storage is null)
            return;

        string fullname = Path.Combine(AppFiles.ApiCachePath, GenerateFileName(url));
        SemaphoreSlim fileLock = GetLock(fullname);

        for (int retry = 0; retry <= 10; retry++)
        {
            await fileLock.WaitAsync();

            try
            {
                await Storage.WriteAllTextAsync(fullname, data, CancellationToken.None);
                MaybeSchedulePrune();
                return;
            }
            catch (Exception) when (retry < 10) { }
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

    private static void MaybeSchedulePrune()
    {
        long now = DateTime.UtcNow.Ticks;
        long last = Interlocked.Read(ref _lastPruneTicksUtc);
        if (new TimeSpan(now - last) < _pruneInterval)
            return;

        // Single-flight: only one Prune at a time.
        if (Interlocked.CompareExchange(ref _pruneInFlight, 1, 0) != 0)
            return;

        Interlocked.Exchange(ref _lastPruneTicksUtc, now);
        _ = Task.Run(() =>
        {
            try
            {
                PruneCache();
            }
            catch
            {
                // best-effort — don't let a prune error bubble out of the writer path.
            }
            finally
            {
                Interlocked.Exchange(ref _pruneInFlight, 0);
            }
        });
    }

    internal static void PruneCache(string cachePath, long maxSizeBytes)
    {
        // Cache lives behind IStorage so remote drivers (S3/R2/SMB/NFS) hit
        // the same path-validation seam as everything else. Don't reach
        // through to System.IO.* here — that would silently bypass the
        // facade if the cache path ever moved off local disk.
        IStorage storage = Storage;
        IReadOnlyList<StorageEntry> entries;
        try
        {
            entries = storage.List(cachePath, pattern: null, recursive: false);
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        StorageEntry[] files = entries
            .Where(e => !e.IsDirectory)
            .OrderBy(e => e.LastModified)
            .ToArray();

        long totalSize = files.Sum(f => f.SizeBytes);

        foreach (StorageEntry file in files)
        {
            if (totalSize <= maxSizeBytes)
                break;

            try
            {
                totalSize -= file.SizeBytes;
                storage.Delete(file.Path);
            }
            catch (Exception)
            {
                // File may be locked or already removed; skip.
            }
        }
    }
}
