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

using NoMercy.Encoder.Output;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

internal static class OutputStrategyFactoryTestHelper
{
    public static OutputStrategyFactory Create() =>
        new(strategies:
        [
            new HlsOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new MkvOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new Mp4OutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new DashOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new Mp3OutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new FlacOutputStrategy(storage: TestStorageFactory.CreateLocal()),
            new OggOutputStrategy(storage: TestStorageFactory.CreateLocal()),
        ]);
}
