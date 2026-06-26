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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Strategies;

namespace NoMercy.Encoder.Orchestration;

public interface IStrategyResolver
{
    /// <summary>
    /// Returns the strategy registered for <paramref name="format"/> +
    /// <paramref name="mode"/>, or <c>null</c> when no strategy is registered
    /// (e.g. 2-pass DASH before that strategy ships). Callers must handle null
    /// by returning a validation error instead of crashing.
    /// </summary>
    IEncodingStrategy? Resolve(OutputFormat format, EncodeMode mode);
}
