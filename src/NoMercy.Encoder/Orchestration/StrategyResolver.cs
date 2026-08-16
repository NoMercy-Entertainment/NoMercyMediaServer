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

/// <summary>
/// Picks a strategy from the DI-registered <see cref="IEncodingStrategy"/>
/// implementations. Plugin-supplied strategies appear in the same enumerable
/// and participate in resolution automatically (last registration wins).
/// </summary>
public class StrategyResolver(IEnumerable<IEncodingStrategy> strategies) : IStrategyResolver
{
    private readonly IReadOnlyList<IEncodingStrategy> _strategies = [.. strategies];

    public IEncodingStrategy? Resolve(OutputFormat format, EncodeMode mode)
    {
        // Walk in reverse so later registrations (plugins) override built-ins.
        for (int i = _strategies.Count - 1; i >= 0; i--)
        {
            IEncodingStrategy strategy = _strategies[i];
            if (strategy.Format == format && strategy.EncodeMode == mode)
                return strategy;
        }

        return null;
    }
}
