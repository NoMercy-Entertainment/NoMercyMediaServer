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
        new([
            new HlsOutputStrategy(TestStorageFactory.CreateLocal()),
            new MkvOutputStrategy(TestStorageFactory.CreateLocal()),
            new Mp4OutputStrategy(TestStorageFactory.CreateLocal()),
            new DashOutputStrategy(TestStorageFactory.CreateLocal()),
            new Mp3OutputStrategy(TestStorageFactory.CreateLocal()),
            new FlacOutputStrategy(TestStorageFactory.CreateLocal()),
            new OggOutputStrategy(TestStorageFactory.CreateLocal()),
        ]);
}
