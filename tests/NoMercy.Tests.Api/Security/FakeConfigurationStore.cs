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

using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Tests.Api.Security;

public class FakeConfigurationStore : IConfigurationStore
{
    private readonly Dictionary<string, string> _values = [];

    public string? GetValue(string key) => _values.GetValueOrDefault(key);

    public void SetValue(string key, string value) => _values[key] = value;

    public Task SetValueAsync(string key, string value, Guid? modifiedBy = null)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public bool HasKey(string key) => _values.ContainsKey(key);
}
