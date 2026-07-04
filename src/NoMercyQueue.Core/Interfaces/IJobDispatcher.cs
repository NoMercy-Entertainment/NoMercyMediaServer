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

namespace NoMercyQueue.Core.Interfaces;

public interface IJobDispatcher
{
    void Dispatch(IShouldQueue job);
    void Dispatch(IShouldQueue job, string onQueue, int priority);
    void DispatchChild(
        IShouldQueue job,
        string onQueue,
        int priority,
        int parentJobId,
        string groupTag
    );
}
