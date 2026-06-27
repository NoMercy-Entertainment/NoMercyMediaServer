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

using NoMercy.Storage;

namespace NoMercy.NmSystem.Auth;

/// <summary>
/// <see cref="ICredentialResolver"/> backed by <see cref="CredentialManager"/>.
/// </summary>
public class CredentialResolver : ICredentialResolver
{
    public (string AccessKey, string SecretKey)? Resolve(string credentialsRef)
    {
        UserPass? cred = CredentialManager.Credential(credentialsRef);
        if (cred is null)
            return null;

        // Treat an entry with empty fields the same as a missing entry — a
        // saved-but-blank credential stored an empty UserPass in the secrets
        // store, and propagating that to the AWS SDK / WebDAV client surfaces
        // as "Credential access key has length 0" instead of falling through
        // to the default credential chain or anonymous-mode wiring.
        if (string.IsNullOrEmpty(cred.Username) && string.IsNullOrEmpty(cred.Password))
            return null;

        return (cred.Username, cred.Password);
    }
}
