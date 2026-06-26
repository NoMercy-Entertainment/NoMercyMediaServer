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

// Shared interface lives in NoMercy.Resources. This file is kept so that
// code inside NoMercy.Encoder can continue to use IResourceBudget without
// changing import statements — the global alias pulls it into scope.
global using IResourceBudget = NoMercy.Resources.IResourceBudget;
