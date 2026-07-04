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

namespace NoMercy.Encoder.Execution;

/// <summary>
/// Platform abstraction over the OS process suspend/resume primitives so the
/// throttle bookkeeping is unit-testable without invoking real OS calls.
/// </summary>
public interface IProcessSuspender
{
    void Suspend(int processId);

    void Resume(int processId);
}
