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

using System.Security.Cryptography;

namespace NoMercy.DiscFormat.Abstractions.Disc;

/// A disc's stable identity, derived from the disc's own structural bytes (DVD VMG/VTS headers,
/// or the Blu-ray AACS Volume ID plus the BDMV skeleton) — never from a folder name or a title
/// slug. Two physically different discs produce different ids even when mounted under the same
/// path, which is what makes a swapped disc detectable.
public sealed record DiscIdentity(string Id, string Provider, string StructuralHash)
{
    /// Hashes the disc's structural skeleton into a provider-prefixed id. The caller chooses what
    /// "structure" means per provider; this never inspects content, only digests the bytes it is
    /// handed, so the same disc always hashes the same and a different disc never collides.
    public static DiscIdentity FromStructuralBytes(string provider, ReadOnlySpan<byte> structure)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(structure));
        return new DiscIdentity($"{provider}:{hash}", provider, hash);
    }
}
