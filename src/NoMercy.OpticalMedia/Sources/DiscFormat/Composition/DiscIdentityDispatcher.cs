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

using NoMercy.DiscFormat.Abstractions.Disc;

namespace NoMercy.DiscFormat.Composition;

/// Routes an identity read to the one registered reader that handles the disc kind, the same shape
/// as the transpiler dispatcher. Adding a disc kind's identity source is a registration, not a
/// switch here. The Blu-ray reader is registered separately (native dependency), so a host that
/// only wires DVD still resolves a working dispatcher for DVD requests.
public sealed class DiscIdentityDispatcher
{
    private readonly IReadOnlyList<IDiscIdentityReader> _readers;

    public DiscIdentityDispatcher(IEnumerable<IDiscIdentityReader> readers)
    {
        _readers = readers.ToList();
    }

    public DiscIdentity Read(DiscTranspileRequest request)
    {
        IDiscIdentityReader reader =
            _readers.FirstOrDefault(candidate => candidate.CanHandle(request.Kind))
            ?? throw new NotSupportedException(
                $"no identity reader handles disc kind '{request.Kind}'"
            );

        return reader.Read(request);
    }
}
