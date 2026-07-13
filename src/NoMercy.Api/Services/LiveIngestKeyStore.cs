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

namespace NoMercy.Api.Services;

/// <inheritdoc />
public class LiveIngestKeyStore : ILiveIngestKeyStore
{
    // An ingest key must outlive any single playback session's whole run so a
    // long transcode's later range requests never 401 mid-stream — the failure
    // mode a short-lived Keycloak token caused. It still never lingers: the idle
    // reaper caps a session well under this, EndSessionAsync revokes explicitly,
    // and this absolute ceiling only backstops a session that dies without a
    // clean teardown. Comfortably longer than the longest single file.
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(12);

    private sealed record Entry(string ServedPath, DateTime ExpiresAtUtc, string? SessionId);

    private readonly ConcurrentDictionary<string, Entry> _byKey = new(StringComparer.Ordinal);

    public string Issue(string servedPath)
    {
        PruneExpired();

        // 256 bits of entropy, base64url so the value is safe to drop verbatim
        // into an ffmpeg "-headers" line (no CR/LF, no reserved chars).
        string key = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        _byKey[key] = new(servedPath, DateTime.UtcNow.Add(AbsoluteLifetime), SessionId: null);
        return key;
    }

    public void BindSession(string key, string sessionId)
    {
        if (_byKey.TryGetValue(key, out Entry? entry))
            _byKey[key] = entry with { SessionId = sessionId };
    }

    public bool TryValidate(string key, string requestPath)
    {
        if (string.IsNullOrEmpty(key) || !_byKey.TryGetValue(key, out Entry? entry))
            return false;

        if (DateTime.UtcNow > entry.ExpiresAtUtc)
        {
            _byKey.TryRemove(key, out _);
            return false;
        }

        // The serving middleware decodes the path; match both the raw request
        // path and its decoded form so a key never fails on a percent-encoded
        // segment the source URL carried.
        string decoded = Uri.UnescapeDataString(requestPath);
        return string.Equals(entry.ServedPath, requestPath, StringComparison.Ordinal)
            || string.Equals(entry.ServedPath, decoded, StringComparison.Ordinal);
    }

    public void RevokeSession(string sessionId)
    {
        foreach (KeyValuePair<string, Entry> kvp in _byKey)
        {
            if (string.Equals(kvp.Value.SessionId, sessionId, StringComparison.Ordinal))
                _byKey.TryRemove(kvp.Key, out _);
        }
    }

    private void PruneExpired()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, Entry> kvp in _byKey)
        {
            if (now > kvp.Value.ExpiresAtUtc)
                _byKey.TryRemove(kvp.Key, out _);
        }
    }
}
