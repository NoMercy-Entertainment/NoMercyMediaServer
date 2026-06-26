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

namespace NoMercy.Setup.Boot;

public class DeferredTasks
{
    public bool ApiKeysLoaded { get; set; }
    public bool Authenticated { get; set; }
    public bool NetworkDiscovered { get; set; }
    public bool Registered { get; set; }
    public bool SeedsRun { get; set; }
    public bool AllCompleted { get; set; }
}
