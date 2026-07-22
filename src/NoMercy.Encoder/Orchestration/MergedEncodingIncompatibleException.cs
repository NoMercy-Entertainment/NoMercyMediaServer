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

namespace NoMercy.Encoder.Orchestration;

/// <summary>
/// Thrown by <see cref="IEncodingOrchestrator.DecomposeMergedAsync"/> when the
/// requests handed to it cannot be coordinated into a single encode — e.g. two
/// presets resolve to different output containers/formats, or one of them
/// fails to plan. Callers (<c>VideoEncodeJob</c>) catch this and fall back to
/// dispatching each preset independently, exactly as before the smart
/// orchestrator existed, rather than crashing the whole folder's encode.
/// </summary>
public sealed class MergedEncodingIncompatibleException(string message) : Exception(message: message);
