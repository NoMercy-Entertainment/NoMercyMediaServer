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

namespace NoMercy.Encoder.Output;

public interface IOutputStrategyFactory
{
    /// <summary>
    /// Resolves the <see cref="IOutputStrategy"/> registered for the given
    /// output format. Plugins can replace built-in strategies by registering
    /// an <see cref="IOutputStrategy"/> for the same <see cref="OutputFormat"/>
    /// — the last registration wins, matching how <c>IEncodingStrategy</c>
    /// resolution works.
    /// </summary>
    IOutputStrategy Resolve(OutputFormat format);
}
