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

using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem.Logging;

public static class LogCache
{
    private const int MaxEntries = 7;
    private static readonly Dictionary<string, List<LogEntry>?> Cache = new();

    public static bool TryGetCachedEntries(string filePath, out List<LogEntry>? cachedEntries)
    {
        return Cache.TryGetValue(key: filePath, value: out cachedEntries);
    }

    public static void AddToCache(string filePath, List<LogEntry>? entries)
    {
        if (!Cache.ContainsKey(key: filePath) && Cache.Count >= MaxEntries)
            Cache.Remove(key: Cache.Keys.First());

        Cache[key: filePath] = entries;
    }
}
