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

// NoMercy.Data.Activity.IActivityLogger is an alias for the canonical interface
// defined in NoMercy.Database.Activity. Callers within NoMercy.Data and NoMercy.Api
// can continue using `using NoMercy.Data.Activity;` — the type is the same object.
namespace NoMercy.Data.Activity;

public interface IActivityLogger : Database.Activity.IActivityLogger { }
