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

public class OutputStrategyFactory(IEnumerable<IOutputStrategy> strategies) : IOutputStrategyFactory
{
    // Walk in reverse so later DI registrations (plugins) shadow built-ins.
    private readonly IReadOnlyList<IOutputStrategy> _strategies = strategies.Reverse().ToList();

    public IOutputStrategy Resolve(OutputFormat format)
    {
        foreach (IOutputStrategy strategy in _strategies)
        {
            if (strategy.Format == format)
                return strategy;
        }

        throw new ArgumentOutOfRangeException(
            nameof(format),
            format,
            $"No IOutputStrategy is registered for format {format}"
        );
    }
}
