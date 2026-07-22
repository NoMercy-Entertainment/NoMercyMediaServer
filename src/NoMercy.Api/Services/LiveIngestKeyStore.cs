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
    private static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(hours: 12);

    private sealed record Entry(string PathPrefix, DateTime ExpiresAtUtc, string? SessionId);

    private readonly ConcurrentDictionary<string, Entry> _byKey = new(comparer: StringComparer.Ordinal);

    public string Issue(string servedPath)
    {
        PruneExpired();

        // Authorize the served file's whole folder, not just the exact file. A
        // NoMercy-encoded source is an HLS master (".NoMercy.m3u8") whose variant
        // playlists, segments, audio renditions and subtitles all live under the
        // same episode/movie folder, and ffmpeg's self-ingest follows every one of
        // those nested URLs. Scoping to the file alone 401s them all. The folder is
        // one title's content — still far tighter than the old library-wide bearer,
        // and the request is loopback + ingest-port gated regardless.
        int lastSlash = servedPath.LastIndexOf(value: '/');
        string prefix = lastSlash >= 0 ? servedPath[..(lastSlash + 1)] : servedPath;

        // 256 bits of entropy, base64url so the value is safe to drop verbatim
        // into an ffmpeg "-headers" line (no CR/LF, no reserved chars).
        string key = Convert
            .ToBase64String(inArray: RandomNumberGenerator.GetBytes(count: 32))
            .Replace(oldChar: '+', newChar: '-')
            .Replace(oldChar: '/', newChar: '_')
            .TrimEnd(trimChar: '=');

        _byKey[key: key] = new(PathPrefix: prefix, ExpiresAtUtc: DateTime.UtcNow.Add(value: AbsoluteLifetime), SessionId: null);
        return key;
    }

    public void BindSession(string key, string sessionId)
    {
        if (_byKey.TryGetValue(key: key, value: out Entry? entry))
            _byKey[key: key] = entry with { SessionId = sessionId };
    }

    public bool TryValidate(string key, string requestPath)
    {
        if (string.IsNullOrEmpty(value: key) || !_byKey.TryGetValue(key: key, value: out Entry? entry))
            return false;

        if (DateTime.UtcNow > entry.ExpiresAtUtc)
        {
            _byKey.TryRemove(key: key, value: out _);
            return false;
        }

        // The request path must sit under the authorized folder prefix. The
        // serving middleware decodes the path, so match both the raw request path
        // and its decoded form — the prefix ends in '/', so this is a true folder
        // boundary, not a "/dir" matching "/dir-other" substring slip.
        string decoded = Uri.UnescapeDataString(stringToUnescape: requestPath);
        return requestPath.StartsWith(value: entry.PathPrefix, comparisonType: StringComparison.Ordinal)
            || decoded.StartsWith(value: entry.PathPrefix, comparisonType: StringComparison.Ordinal);
    }

    public void RevokeSession(string sessionId)
    {
        foreach (KeyValuePair<string, Entry> kvp in _byKey)
        {
            if (string.Equals(a: kvp.Value.SessionId, b: sessionId, comparisonType: StringComparison.Ordinal))
                _byKey.TryRemove(key: kvp.Key, value: out _);
        }
    }

    private void PruneExpired()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, Entry> kvp in _byKey)
        {
            if (now > kvp.Value.ExpiresAtUtc)
                _byKey.TryRemove(key: kvp.Key, value: out _);
        }
    }
}
