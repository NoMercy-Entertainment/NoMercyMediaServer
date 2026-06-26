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

// This assembly exercises process-global static state (e.g. HttpClientProvider's
// _factory, the ApiKeyStore singleton, Encoder service statics). xUnit runs test
// collections in parallel by default, so one class can mutate/Reset that static
// while another reads it, producing intermittent failures that pass in isolation.
// Serialize collections so the shared statics are never touched concurrently.
// (The statics themselves are slated for replacement by scoped DI in the wider
// refactor; this keeps the suite deterministic until then.)
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
