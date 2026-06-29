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

using System;
using Microsoft.Extensions.Logging;

namespace NoMercy.NmSystem.Logging;

/// <summary>
/// A structured log record handed to the JSON file sink and the record callback
/// (dashboard live-log / event bus). Carries the resolved category so consumers do
/// not need to re-resolve it.
/// </summary>
public sealed record NoMercyLogRecord(
    DateTime Timestamp,
    LogLevel Level,
    string CategoryKey,
    string CategoryName,
    string Message,
    string? Scope,
    string? Exception
);
