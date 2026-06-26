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

using NoMercy.Encoder.Execution;

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Named alias for the execution stage contract. Resolving
/// <see cref="IExecutionStage"/> from DI returns the same instance as
/// <see cref="ExecuteStage"/>. Strategies and plugins that need to
/// swap in a custom execution implementation can register a replacement
/// against this interface without coupling to the concrete class name.
/// </summary>
public interface IExecutionStage : IPipelineStage<ExecuteInput, ExecutionResult[]> { }
