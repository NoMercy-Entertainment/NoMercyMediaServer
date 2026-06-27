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

namespace NoMercy.Providers.Abstractions;

/// <summary>
/// Marker interface for the external HTTP provider clients built on
/// <see cref="ExternalApiClient"/>. Lets callers and DI treat any provider
/// uniformly (e.g. for registration or disposal) without depending on a
/// concrete client type.
/// </summary>
public interface IExternalProvider : IDisposable { }
