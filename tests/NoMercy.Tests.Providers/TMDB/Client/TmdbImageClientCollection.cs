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

using Xunit;

namespace NoMercy.Tests.Providers.TMDB.Client;

/// <summary>
/// TmdbImageClient holds its IStorage as static state (Initialize sets a shared
/// field). Tests that call Initialize must not run in parallel, or one class's
/// storage clobbers another's mid-call. Grouping them in one collection forces
/// serial execution.
/// </summary>
[CollectionDefinition("TmdbImageClient")]
public class TmdbImageClientCollection;
