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

namespace NoMercy.Storage;

/// <summary>
/// Wraps a path that is safe to hand to a child process (e.g. ffmpeg).
/// <see cref="LocalStorage"/> returns the validated path as-is with a
/// no-op dispose. Future remote drivers (SMB / NFS / S3 / R2) will
/// stage the remote object into a temp file and clean it up on dispose.
/// </summary>
public sealed class LocalPathLease : IAsyncDisposable
{
    private readonly Func<ValueTask>? _onDispose;
    private bool _disposed;

    public string Path { get; }

    public LocalPathLease(string path, Func<ValueTask>? onDispose = null)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        _onDispose = onDispose;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_onDispose is not null)
            await _onDispose();
    }
}
