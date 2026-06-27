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

// Serialize xUnit test collections across every test assembly. The suite
// exercises process-global mutable statics (ClaimsPrincipalExtensions._users,
// HttpClientProvider._factory, the ApiKeyStore singleton, QueueRunner.Current,
// Encoder service statics, ...). With collection parallelism one class can
// mutate/Reset such a static while another reads it, yielding intermittent
// failures that pass in isolation. Serializing collections makes the full suite
// deterministic. (The statics are slated for scoped DI in the wider refactor;
// this is the bridge until then.)
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
