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

namespace NoMercy.DiscFormat.Abstractions.Disc;

/// Reads a disc's structural identity for one disc kind. Implementations live next to the parser
/// for their kind (DVD reads IFO headers; Blu-ray reads the AACS Volume ID via the native lib),
/// so the native dependency stays quarantined. The pipeline picks the reader whose CanHandle
/// matches the detected kind, the same dispatch shape as the transpilers.
public interface IDiscIdentityReader
{
    bool CanHandle(DiscKind kind);

    DiscIdentity Read(DiscTranspileRequest request);
}
