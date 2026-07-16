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

namespace NoMercy.Encoder.Reconciliation;

/// <summary>What the reconciler decided a redispatch of an already-encoded
/// file should actually do.</summary>
public enum ReconciliationAction
{
    /// <summary>Every desired output is present, valid, and matches the
    /// resolved profile. No ffmpeg command is built at all.</summary>
    Skip,

    /// <summary>The profile is unchanged (or predates fingerprinting) but one
    /// or more desired outputs are missing or invalid. Only the missing
    /// pieces are re-run.</summary>
    Partial,

    /// <summary>The profile genuinely changed since this output was produced
    /// (or the caller forced it, or the existing bundle is structurally
    /// incomplete). The whole file is re-encoded.</summary>
    Full,
}
