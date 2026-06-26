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

namespace NoMercy.Encoder.Errors;

/// <summary>
/// Severity attached to an <see cref="EncoderRule"/>. The dashboard
/// renders Error as a red chip, Warning as yellow, Info as a neutral
/// pill. <see cref="ValidationEnvelope.Valid"/> is true only when no
/// rule of severity Error is present.
/// </summary>
public enum EncoderRuleSeverity
{
    Info,
    Warning,
    Error,
}
