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

// The Api suite drives a shared WebApplicationFactory and shares process-wide
// statics (the ClaimsPrincipleExtensions _users cache + the on-disk test DB).
// Worker/coordinator tests Reset() the user cache, so parallel classes can read
// it mid-mutation and get spurious 403s. Serialize collections; combined with
// the per-class user-cache reseed in NoMercyApiFactory this keeps the suite
// deterministic. (The static cache is slated for a scoped IUserCache in the
// wider refactor.)
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
