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

// Mirrors the production-side global alias so test files don't need a per-file
// 'using EncodeMode = NoMercy.Encoder.Profiles.EncodeMode;' line. The legacy
// NoMercy.Encoder.Codecs.EncodeMode is gone; tests historically referenced it
// either bare or via the V2EncodeMode alias.
global using EncodeMode = NoMercy.Encoder.Profiles.EncodeMode;
