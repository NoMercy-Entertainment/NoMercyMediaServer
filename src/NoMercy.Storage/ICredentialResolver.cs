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
/// Resolves a named credential reference to an (accessKey, secretKey) pair.
/// Implemented in a higher-level project that has access to the secrets store;
/// injected into <see cref="IStorageFactory"/> at DI registration time.
/// </summary>
public interface ICredentialResolver
{
    /// <summary>
    /// Returns the (accessKey, secretKey) for <paramref name="credentialsRef"/>,
    /// or <c>null</c> if the key is not found.
    /// </summary>
    (string AccessKey, string SecretKey)? Resolve(string credentialsRef);
}
