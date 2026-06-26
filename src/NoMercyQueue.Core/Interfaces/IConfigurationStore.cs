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

public interface IConfigurationStore
{
    string? GetValue(string key);
    void SetValue(string key, string value);
    Task SetValueAsync(string key, string value, Guid? modifiedBy = null);
    bool HasKey(string key);
}
