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

namespace NoMercy.Plugin.Samples.Failures;

// Registered by ServiceRegistratorPlugin.RegisterServices — its presence in a
// service collection proves the host actually invoked the discovered
// IPluginServiceRegistrator rather than merely finding it via reflection.
public sealed class FailuresPluginMarker;
