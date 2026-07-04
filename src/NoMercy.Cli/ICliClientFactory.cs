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
/// Creates <see cref="ICliClient"/> instances bound to a specific named pipe or
/// Unix socket path. The path is only known at command-invocation time, so the
/// client is produced per command rather than registered as a singleton.
/// </summary>
internal interface ICliClientFactory
{
    ICliClient Create(string? pipeNameOrSocketPath);
}
