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

using NoMercy.NmSystem.Extensions;

namespace NoMercy.MediaProcessing.Common;

public class BaseManager : IBaseManager
{
    public string BaseUrl(string title, DateTime? releaseDate)
    {
        return "/" + string.Concat(title, ".(", releaseDate.ParseYear(), ")").CleanFileName();
    }

    public string BaseUrl(string name)
    {
        return string.Concat(name[0], "/", name).CleanFileName();
    }
}
