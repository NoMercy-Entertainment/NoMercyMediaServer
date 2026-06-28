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
namespace NoMercy.Cli;

/// <summary>
/// Abstraction over the management IPC client used by the CLI commands. Depending
/// on this interface (created through <see cref="ICliClientFactory"/>) instead of
/// constructing <see cref="CliClient"/> directly lets commands be unit-tested with
/// a fake client.
/// </summary>
internal interface ICliClient : IDisposable
{
    Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class;

    Task<string?> GetRawAsync(string path, CancellationToken cancellationToken = default);

    Task<bool> PostAsync(
        string path,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    );

    Task<T?> PostAsync<T>(
        string path,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    )
        where T : class;

    Task<bool> PutAsync(
        string path,
        HttpContent? content = null,
        CancellationToken cancellationToken = default
    );
}
