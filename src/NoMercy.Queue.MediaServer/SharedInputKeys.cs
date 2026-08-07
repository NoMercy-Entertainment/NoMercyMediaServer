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

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// How shared job input is named. Written here alone: the dispatcher, the job and
/// the pass that compacts old rows all have to agree on the key, and a second
/// spelling of it would silently orphan every blob one of them wrote.
/// </summary>
public static class SharedInputKeys
{
    public static string Release(Guid releaseId) => $"release:{releaseId}";
}
